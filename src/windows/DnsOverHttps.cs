using System;
using System.IO;
using System.Globalization;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

public static class DnsOverHttps {
 static readonly SemaphoreSlim slots=new SemaphoreSlim(8);
 static async Task Wait(Task t){if(await Task.WhenAny(t,Task.Delay(10000))!=t)throw new IOException("DNS HTTPS timeout");await t;}
 static async Task<byte[]> Read(Stream s,int n){var t=SocksBridge.Read(s,n);await Wait(t);return await t;}
 static async Task<string> Line(Stream s){var b=new MemoryStream();while(b.Length<16384){byte[] v=await Read(s,1);b.WriteByte(v[0]);if(v[0]==10){byte[] a=b.ToArray();if(a.Length<2||a[a.Length-2]!=13)throw new IOException("Invalid HTTP line");return Encoding.ASCII.GetString(a,0,a.Length-2);}}throw new IOException("HTTP header too large");}
 public static async Task<byte[]> ReadResponse(Stream s){
  string status=await Line(s);string[] parts=status.Split(' ');if(parts.Length<2||parts[1]!="200")throw new IOException("DNS HTTPS status: "+status);
  int length=-1,total=0;bool chunked=false,dns=false;
  while(true){string line=await Line(s);total+=line.Length+2;if(total>16384)throw new IOException("HTTP headers too large");if(line=="")break;int split=line.IndexOf(':');if(split<1)throw new IOException("Invalid HTTP header");string key=line.Substring(0,split).Trim().ToLowerInvariant(),value=line.Substring(split+1).Trim().ToLowerInvariant();
   if(key=="content-length"){if(!int.TryParse(value,out length)||length<0||length>65535)throw new IOException("Invalid DNS body length");}
   if(key=="transfer-encoding")chunked=value=="chunked";
   if(key=="content-type")dns=value.Split(';')[0].Trim()=="application/dns-message";
  }
  if(!dns)throw new IOException("Unexpected DNS content type");
  if(chunked){using(var body=new MemoryStream()){while(true){string line=await Line(s);int n;if(!int.TryParse(line.Split(';')[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out n)||n<0||body.Length+n>65535)throw new IOException("Invalid chunk size");if(n==0)break;byte[] chunk=await Read(s,n);body.Write(chunk,0,n);byte[] end=await Read(s,2);if(end[0]!=13||end[1]!=10)throw new IOException("Invalid chunk terminator");}return body.ToArray();}}
  if(length>=0)return await Read(s,length);
  using(var body=new MemoryStream()){byte[] buffer=new byte[4096];while(true){var read=s.ReadAsync(buffer,0,buffer.Length);await Wait(read);int n=await read;if(n==0)break;if(body.Length+n>65535)throw new IOException("DNS body too large");body.Write(buffer,0,n);}return body.ToArray();}
 }
 public static async Task<byte[]> Resolve(int phoneTcp,byte[] query){await slots.WaitAsync();try{return await ResolveCore(phoneTcp,query);}finally{slots.Release();}}
 static async Task<byte[]> ResolveCore(int phoneTcp,byte[] query){
  if(query.Length<12||query.Length>65535||(query[2]&128)!=0)throw new IOException("Invalid DNS query");
  const string host="cloudflare-dns.com";
  using(var c=await BridgeDiagnostics.Open(phoneTcp,host,443))using(var tls=new SslStream(c.GetStream(),false)){
   await Wait(tls.AuthenticateAsClientAsync(host,null,SslProtocols.Tls12,false));
   byte[] headers=Encoding.ASCII.GetBytes("POST /dns-query HTTP/1.1\r\nHost: "+host+"\r\nContent-Type: application/dns-message\r\nAccept: application/dns-message\r\nConnection: close\r\nContent-Length: "+query.Length+"\r\n\r\n");
   await Wait(tls.WriteAsync(headers,0,headers.Length));await Wait(tls.WriteAsync(query,0,query.Length));byte[] answer=await ReadResponse(tls);
   if(answer.Length<12||(answer[2]&128)==0)throw new IOException("Invalid DNS answer");answer[0]=query[0];answer[1]=query[1];return answer;
  }
 }
}
