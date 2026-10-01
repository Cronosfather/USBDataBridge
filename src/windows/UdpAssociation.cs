using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed class UdpAssociation : IDisposable {
 readonly UdpClient udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
 readonly int phoneTcp,phoneUdp;readonly Action<string> report;readonly Func<byte[],Task<byte[]>> resolver;
 readonly SemaphoreSlim sendLock=new SemaphoreSlim(1),dnsSlots=new SemaphoreSlim(4);
 TcpClient transport;IPEndPoint endpoint;volatile bool closed;
 public int Port{get{return ((IPEndPoint)udp.Client.LocalEndPoint).Port;}}
 public UdpAssociation(int tcp,int relay,Action<string> log,Func<byte[],Task<byte[]>> dns=null){phoneTcp=tcp;phoneUdp=relay;report=log;resolver=dns??(query=>DnsOverHttps.Resolve(phoneTcp,query));}
 public async Task Run(Stream control){Task packets=Receive();Task ending=Task.Run(async()=>{byte[] b=new byte[128];while(await control.ReadAsync(b,0,b.Length)>0){}});await Task.WhenAny(packets,ending);Dispose();control.Close();try{await Task.WhenAll(packets,ending);}catch{}}
 async Task Receive(){while(!closed){var d=await udp.ReceiveAsync();if(!IPAddress.IsLoopback(d.RemoteEndPoint.Address))continue;if(endpoint==null)endpoint=d.RemoteEndPoint;if(!endpoint.Equals(d.RemoteEndPoint)||d.Buffer.Length<7||d.Buffer[0]!=0||d.Buffer[1]!=0||d.Buffer[2]!=0)continue;Handle(d.Buffer);}}
 static int Payload(byte[] data){int n=data[3]==1?4:data[3]==4?16:data[3]==3&&data.Length>=5?1+data[4]:-1;int offset=4+n+2;return n<0||offset>data.Length?-1:offset;}
 static bool PublicDns(byte[] b,int offset){if(offset!=10||b[3]!=1||b[8]!=0||b[9]!=53)return false;string ip=new IPAddress(new byte[]{b[4],b[5],b[6],b[7]}).ToString();return ip=="1.1.1.1"||ip=="1.0.0.1"||ip=="8.8.8.8"||ip=="8.8.4.4";}
 async void Handle(byte[] data){int offset=Payload(data);if(offset<0)return;
  if(PublicDns(data,offset)){if(!dnsSlots.Wait(0))return;try{byte[] query=new byte[data.Length-offset];Buffer.BlockCopy(data,offset,query,0,query.Length);byte[] answer=await resolver(query);byte[] reply=new byte[offset+answer.Length];if(reply.Length>65535)throw new IOException("DNS response too large");Buffer.BlockCopy(data,0,reply,0,offset);Buffer.BlockCopy(answer,0,reply,offset,answer.Length);if(!closed)await udp.SendAsync(reply,reply.Length,endpoint);}catch(Exception e){Log("DNS HTTPS: "+e.Message);}finally{dnsSlots.Release();}return;}
  await sendLock.WaitAsync();try{if(closed)return;if(transport==null){var c=new TcpClient();try{var connect=c.ConnectAsync(IPAddress.Loopback,phoneUdp);await Timed(connect);c.NoDelay=true;var s=c.GetStream();byte[] magic={85,68,66,50};await s.WriteAsync(magic,0,4);var read=SocksBridge.Read(s,4);await Timed(read);byte[] reply=await read;for(int i=0;i<4;i++)if(reply[i]!=magic[i])throw new IOException("UDP handshake mismatch");transport=c;PhoneReplies(c);}catch{c.Close();throw;}}
   int n=data.Length;byte[] length={(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};var stream=transport.GetStream();await stream.WriteAsync(length,0,4);await stream.WriteAsync(data,0,n);
  }catch(Exception e){if(transport!=null){transport.Close();transport=null;}Log("일반 UDP 전달: "+e.Message);}finally{sendLock.Release();}
 }
 static async Task Timed(Task t){if(await Task.WhenAny(t,Task.Delay(5000))!=t)throw new IOException("UDP handshake timeout");await t;}
 async void PhoneReplies(TcpClient c){try{var stream=c.GetStream();while(!closed){byte[] h=await SocksBridge.Read(stream,4);int n=(h[0]<<24)|(h[1]<<16)|(h[2]<<8)|h[3];if(n<7||n>65535)throw new IOException("Invalid UDP reply length");byte[] data=await SocksBridge.Read(stream,n);if(endpoint!=null&&!closed)await udp.SendAsync(data,data.Length,endpoint);}}catch(Exception e){if(!closed)Log("일반 UDP 응답: "+e.Message);}finally{c.Close();if(Object.ReferenceEquals(transport,c))transport=null;}}
 void Log(string s){if(!closed&&report!=null)report(s);}
 public void Dispose(){closed=true;udp.Close();var c=transport;if(c!=null)c.Close();}
}
