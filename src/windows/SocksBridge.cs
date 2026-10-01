using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class SocksBridge : IDisposable {
 readonly int phoneTcp, phoneUdp;
 readonly TcpListener listener;
 readonly ConcurrentDictionary<TcpClient,byte> clients=new ConcurrentDictionary<TcpClient,byte>();
 readonly SemaphoreSlim slots=new SemaphoreSlim(1024),tcpSlots=new SemaphoreSlim(256),udpSlots=new SemaphoreSlim(512),phoneSlots=new SemaphoreSlim(112);
 int activeTcp,activeUdp,activePhone;
 public string CapacityStatus {get{return "활성 TCP "+activeTcp+"/256 · UDP "+activeUdp+"/512 · 휴대폰 TCP "+activePhone+"/112";}}
 volatile bool stopped; readonly Action<string> report;readonly Func<byte[],Task<byte[]>> dnsResolver;
 public SocksBridge(int port,int tcp,int udp,Action<string> log=null,Func<byte[],Task<byte[]>> resolver=null) {report=log;dnsResolver=resolver;phoneTcp=tcp;phoneUdp=udp;listener=new TcpListener(IPAddress.Loopback,port);}
 public void Start(){listener.Start(128); Task.Run((Func<Task>)Accept);}
 public int Port {get{return ((IPEndPoint)listener.LocalEndpoint).Port;}}
 async Task Accept(){while(!stopped){TcpClient c=null;try{c=await listener.AcceptTcpClientAsync();if(!slots.Wait(0)){if(report!=null)report("전체 연결 한도 1024개 도달 · "+CapacityStatus);c.Close();continue;}clients[c]=0;Handle(c);}catch{if(c!=null)c.Close();if(stopped)break;}}}
 // Each accepted session owns and closes all of its transports.
 async void Handle(TcpClient c){TcpClient phone=null;UdpClient udp=null;bool tcpSlot=false,udpSlot=false,phoneSlot=false;try{
  c.NoDelay=true;var s=c.GetStream();byte[] h=await HandshakeRead(s,2);if(h[0]!=5||h[1]==0)return;
  byte[] methods=await HandshakeRead(s,h[1]);if(Array.IndexOf(methods,(byte)0)<0){await s.WriteAsync(new byte[]{5,255},0,2);return;}
  await s.WriteAsync(new byte[]{5,0},0,2);
  byte[] head=await HandshakeRead(s,4);if(head[0]!=5||head[2]!=0)return;
  byte[] addr=await Address(s,head[3]);byte[] port=await HandshakeRead(s,2);
  if(head[1]==1){
   if(!await tcpSlots.WaitAsync(5000)){await Failure(s,"TCP 연결 한도 도달");return;}tcpSlot=true;Interlocked.Increment(ref activeTcp);
   if(!await phoneSlots.WaitAsync(10000)){await Failure(s,"휴대폰 TCP 연결 대기 시간 초과");return;}phoneSlot=true;Interlocked.Increment(ref activePhone);
   phone=new TcpClient();clients[phone]=0;await Connect(phone,phoneTcp);var p=phone.GetStream();
   await p.WriteAsync(new byte[]{5,1,0},0,3);byte[] auth=await Read(p,2);if(auth[0]!=5||auth[1]!=0)throw new IOException("Phone authentication failed");
   byte[] req=Join(head,addr,port);await p.WriteAsync(req,0,req.Length);
   byte[] reply=await Read(p,4);byte[] ra=await Address(p,reply[3]);byte[] rp=await Read(p,2);byte[] all=Join(reply,ra,rp);await s.WriteAsync(all,0,all.Length);if(reply[1]!=0)return;
   await Task.WhenAll(Pump(s,p,phone),Pump(p,s,c));
  }else if(head[1]==3){
   if(!await udpSlots.WaitAsync(5000)){await Failure(s,"UDP 연결 한도 도달");return;}udpSlot=true;Interlocked.Increment(ref activeUdp);
   using(var association=new UdpAssociation(phoneTcp,phoneUdp,report,dnsResolver)){
    int local=association.Port;byte[] ok={5,0,0,1,127,0,0,1,(byte)(local>>8),(byte)local};await s.WriteAsync(ok,0,ok.Length);await association.Run(s);
   }
  }else{await s.WriteAsync(new byte[]{5,7,0,1,0,0,0,0,0,0},0,10);}
 }catch(Exception e){if(report!=null)report("SOCKS 전달: "+e.Message);}finally{byte ignored;c.Close();clients.TryRemove(c,out ignored);if(phone!=null){phone.Close();clients.TryRemove(phone,out ignored);}if(udp!=null)udp.Close();if(phoneSlot){Interlocked.Decrement(ref activePhone);phoneSlots.Release();}if(tcpSlot){Interlocked.Decrement(ref activeTcp);tcpSlots.Release();}if(udpSlot){Interlocked.Decrement(ref activeUdp);udpSlots.Release();}slots.Release();}}
 async Task Failure(Stream s,string reason){if(report!=null)report(reason+" · "+CapacityStatus);await s.WriteAsync(new byte[]{5,1,0,1,0,0,0,0,0,0},0,10);}
 static async Task<byte[]> HandshakeRead(Stream s,int count){var t=Read(s,count);if(await Task.WhenAny(t,Task.Delay(10000))!=t)throw new IOException("SOCKS 준비 요청 시간 초과");return await t;}
 static async Task Connect(TcpClient c,int port){Task t=c.ConnectAsync(IPAddress.Loopback,port);if(await Task.WhenAny(t,Task.Delay(10000))!=t){c.Close();throw new IOException("Phone connection timeout");}await t;c.NoDelay=true;}
 static async Task Pump(Stream source,Stream target,TcpClient targetClient){try{await source.CopyToAsync(target);try{targetClient.Client.Shutdown(SocketShutdown.Send);}catch{}}catch{targetClient.Close();throw;}}
 public static async Task<byte[]> Read(Stream s,int count){byte[] b=new byte[count];int n=0;while(n<count){int r=await s.ReadAsync(b,n,count-n);if(r==0)throw new EndOfStreamException();n+=r;}return b;}
 static async Task<byte[]> Address(Stream s,byte type){if(type==1)return await HandshakeRead(s,4);if(type==4)return await HandshakeRead(s,16);if(type==3){byte[] n=await HandshakeRead(s,1);if(n[0]==0)throw new IOException("Empty address");return Join(n,await HandshakeRead(s,n[0]));}throw new IOException("Invalid address");}
 static byte[] Join(params byte[][] parts){int n=0;foreach(var p in parts)n+=p.Length;byte[] b=new byte[n];int offset=0;foreach(var p in parts){Buffer.BlockCopy(p,0,b,offset,p.Length);offset+=p.Length;}return b;}
 public void Dispose(){stopped=true;listener.Stop();foreach(var c in clients.Keys)c.Close();}
}
