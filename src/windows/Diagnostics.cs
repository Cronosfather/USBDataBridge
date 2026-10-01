using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

public static class BridgeDiagnostics {
 static async Task Wait(Task task,int ms){if(await Task.WhenAny(task,Task.Delay(ms))!=task)throw new IOException("응답 시간 초과");await task;}
 static async Task<byte[]> Read(Stream s,int n){var t=SocksBridge.Read(s,n);await Wait(t,10000);return await t;}
 public static async Task<TcpClient> Open(int proxyPort,string host,int port){
  var c=new TcpClient();bool success=false;
  try{await Wait(c.ConnectAsync(IPAddress.Loopback,proxyPort),3000);var s=c.GetStream();await s.WriteAsync(new byte[]{5,1,0},0,3);byte[] auth=await Read(s,2);if(auth[0]!=5||auth[1]!=0)throw new IOException("USB/SOCKS 준비 응답 오류");
   byte[] name=Encoding.ASCII.GetBytes(host),req=new byte[7+name.Length];req[0]=5;req[1]=1;req[3]=3;req[4]=(byte)name.Length;Buffer.BlockCopy(name,0,req,5,name.Length);req[req.Length-2]=(byte)(port>>8);req[req.Length-1]=(byte)port;
   await s.WriteAsync(req,0,req.Length);byte[] head=await Read(s,4);if(head[0]!=5)throw new IOException("SOCKS 응답 형식 오류");if(head[1]!=0)throw new IOException("휴대폰 외부 연결 실패 · SOCKS 코드 "+head[1]);
   int length=head[3]==1?4:head[3]==4?16:head[3]==3?(await Read(s,1))[0]:0;if(length==0)throw new IOException("SOCKS 주소 오류");await Read(s,length+2);success=true;return c;
  }finally{if(!success)c.Close();}
 }
 static async Task Https(TcpClient c,string host){using(var tls=new SslStream(c.GetStream(),false)){await Wait(tls.AuthenticateAsClientAsync(host,null,SslProtocols.Tls12,false),10000);byte[] request=Encoding.ASCII.GetBytes("HEAD / HTTP/1.1\r\nHost: "+host+"\r\nConnection: close\r\n\r\n");await tls.WriteAsync(request,0,request.Length);byte[] b=await Read(tls,5);if(Encoding.ASCII.GetString(b)!="HTTP/")throw new IOException("HTTPS 응답 오류");}}
 public static async Task SocksHttps(int port,string host){using(var c=await Open(port,host,443))await Https(c,host);}
 public static async Task DirectHttps(string host){using(var c=new TcpClient()){await Wait(c.ConnectAsync(host,443),10000);await Https(c,host);}}
 public static async Task UdpDns(int proxyPort,string dns){using(var c=new TcpClient()){
  await Wait(c.ConnectAsync(IPAddress.Loopback,proxyPort),3000);var s=c.GetStream();await s.WriteAsync(new byte[]{5,1,0},0,3);byte[] auth=await Read(s,2);if(auth[0]!=5||auth[1]!=0)throw new IOException("SOCKS 초기화 실패");await s.WriteAsync(new byte[]{5,3,0,1,0,0,0,0,0,0},0,10);byte[] h=await Read(s,4);if(h[1]!=0||h[3]!=1)throw new IOException("UDP 통로 준비 실패");byte[] address=await Read(s,6);var endpoint=new IPEndPoint(new IPAddress(new byte[]{address[0],address[1],address[2],address[3]}),(address[4]<<8)|address[5]);
  byte[] query={0x73,0x21,1,0,0,1,0,0,0,0,0,0,7,101,120,97,109,112,108,101,3,99,111,109,0,0,1,0,1};
  byte[] ip=IPAddress.Parse(dns).GetAddressBytes(),packet=new byte[10+query.Length];packet[3]=1;Buffer.BlockCopy(ip,0,packet,4,4);packet[9]=53;Buffer.BlockCopy(query,0,packet,10,query.Length);
  using(var u=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){await u.SendAsync(packet,packet.Length,endpoint);var receive=u.ReceiveAsync();await Wait(receive,15000);var answer=await receive;byte[] b=answer.Buffer;if(!answer.RemoteEndPoint.Equals(endpoint)||b.Length<22||b[3]!=1||b[8]!=0||b[9]!=53)throw new IOException("DNS 전달 응답 오류");for(int i=0;i<4;i++)if(b[i+4]!=ip[i])throw new IOException("DNS 서버 주소 불일치");if(b[10]!=query[0]||b[11]!=query[1]||(b[12]&128)==0||(b[13]&15)!=0)throw new IOException("DNS 서버 응답 오류");}
 }}
 public static async Task<bool> Check(string title,Func<Task> probe,Action<string> log){log(title+" 확인 중…");try{await probe();log(title+": 성공");return true;}catch(Exception e){log(title+": 실패 · "+e.Message);return false;}}
 public static async Task Run(int phonePort,int bridgePort,Action<string> log){
  bool ip=await Check("① 휴대폰 외부 IP 통신",async()=>{using(var c=await Open(phonePort,"1.1.1.1",443)){}},log);
  bool phone=await Check("② 휴대폰 도메인·HTTPS",()=>SocksHttps(phonePort,"example.com"),log);
  bool bridged=false,dns=false;
  if(bridgePort>0){bridged=await Check("③ PC→휴대폰 전달",()=>SocksHttps(bridgePort,"example.com"),log);dns=await Check("④ DNS HTTPS 1.1.1.1",()=>UdpDns(bridgePort,"1.1.1.1"),log);if(!dns)dns=await Check("④ DNS HTTPS 1.0.0.1",()=>UdpDns(bridgePort,"1.0.0.1"),log);}else log("③④ 전체 연결이 시작되지 않아 PC 전달 검사를 건너뜁니다.");
  bool system=await Check("⑤ Windows 기본 인터넷 경로",()=>DirectHttps("example.com"),log);
  if(!ip&&!phone)log("진단: USB 통로 또는 휴대폰 외부 통신을 확인해야 합니다.");
  else if(!phone)log("진단: 휴대폰 도메인 조회/HTTPS 검사에 실패했습니다. 해당 사이트의 일시적인 장애일 수도 있습니다.");
  else if(bridgePort>0&&!bridged)log("진단: 휴대폰 외부 통신은 되지만 PC 전달 단계가 실패했습니다.");
  else if(bridgePort>0&&!dns)log("진단: HTTPS 전달은 되지만 DNS HTTPS가 실패했습니다. 이름 조회가 막힐 가능성이 있습니다.");
  else if(!system&&bridged&&dns)log("진단: 전달은 되지만 Windows 기본 경로 검사에 실패했습니다. 가상 경로·DNS·VPN 설정을 확인해야 합니다.");
  else if(system)log("진단: 이 시점의 HTTPS 통신은 성공했습니다. 브라우저 프록시/VPN 또는 특정 사이트 문제를 추가 확인하세요.");
  log("검사 트래픽은 소량의 모바일 데이터를 사용합니다. KT 차감 항목은 이 검사로 판별하지 않습니다.");
 }
}
