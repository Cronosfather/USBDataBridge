using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;

class Launcher : Form {
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 Button start=new Button(),install=new Button(),restore=new Button(),usageButton=new Button();
 Label statusTitle,statusDetail,statusPill;string lastInfo="USB 케이블로 휴대폰을 연결하고 USB 디버깅을 허용하세요.";SocksBridge bridge;Process tunnel,watch;string serial;bool busy,connected,closing;DateTime lastCheck;
 System.Windows.Forms.Timer monitor=new System.Windows.Forms.Timer();
 Label chargeStatus=new Label();UsageCheckForm usageWindow; ConcurrentQueue<string> tunnelMessages=new ConcurrentQueue<string>();
 [STAThread]static void Main(){bool created;using(var mutex=new Mutex(true,"Local\\USBDataBridge-AllApps",out created)){if(!created){MessageBox.Show("프로그램이 이미 실행 중입니다.");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Launcher());}}
 public Launcher(){
  Text="USB Data Bridge";ClientSize=new Size(760,642);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=UiTheme.Font(10);BackColor=UiTheme.Background;
  Controls.Add(UiTheme.Label("USB DATA BRIDGE",28,24,420,22,9,UiTheme.Accent,FontStyle.Bold));
  Controls.Add(UiTheme.Label("휴대폰 데이터로 인터넷 연결",28,53,700,40,23,UiTheme.Ink,FontStyle.Bold));
  Controls.Add(UiTheme.Label("USB로 연결하고, 노트북에서 바로 사용하세요.",28,103,700,26,10,UiTheme.Muted));
  var connection=new UiCard{Location=new Point(28,145),Size=new Size(704,192)};Controls.Add(connection);
  statusTitle=UiTheme.Label("",24,23,650,35,18,UiTheme.Ink,FontStyle.Bold);statusDetail=UiTheme.Label("",24,66,650,49,10,UiTheme.Muted);statusPill=UiTheme.Label("",24,136,330,25,10,UiTheme.Accent,FontStyle.Bold);connection.Controls.Add(statusTitle);connection.Controls.Add(statusDetail);connection.Controls.Add(statusPill);
  UiTheme.Button(start,"인터넷 연결",430,124,246,44,true);connection.Controls.Add(start);
  var setup=new UiCard{Location=new Point(28,355),Size=new Size(704,110)};Controls.Add(setup);
  setup.Controls.Add(UiTheme.Label("처음 사용하나요?",22,16,440,27,12,UiTheme.Ink,FontStyle.Bold));
  setup.Controls.Add(UiTheme.Label("휴대폰 앱을 설치한 뒤 모바일 데이터를 켜 주세요.\n휴대폰의 USB 디버깅 허용 창에서 ‘허용’을 누르세요.",22,49,440,45,9,UiTheme.Muted));
  UiTheme.Button(install,"휴대폰 앱 설치",486,35,196,40,false);setup.Controls.Add(install);
  UiTheme.Button(usageButton,"데이터 사용량 확인",28,484,220,42,false);Controls.Add(usageButton);
  UiTheme.Button(restore,"연결 복구",262,484,174,42,false);Controls.Add(restore);
  chargeStatus=UiTheme.Label("데이터 차감 구분은 통신사 사용량으로 확인하세요.",28,541,704,30,9,UiTheme.Muted);Controls.Add(chargeStatus);
  Controls.Add(UiTheme.Label("기본 USB 테더링과 핫스팟은 꺼 주세요. 연결 복구는 이 앱의 인터넷 경로를 정리합니다.\nWindows 10/11 · Android 8 이상 · v2.1.0",28,583,704,45,8,UiTheme.Muted));
  start.Click+=async delegate{await Action(connected?(Func<Task>)Disconnect:Connect);};install.Click+=async delegate{await Action(Install);};restore.Click+=async delegate{await Action(Disconnect);};
  usageButton.Click+=delegate{if(usageWindow==null||usageWindow.IsDisposed){usageWindow=new UsageCheckForm();usageWindow.ResultChanged+=delegate(string text){chargeStatus.Text="조회값 비교 완료 · 세부 결과는 사용량 확인 창에서 볼 수 있어요.";};}if(!usageWindow.Visible)usageWindow.Show(this);usageWindow.BringToFront();};
  monitor.Interval=2000;monitor.Tick+=async delegate{if(!connected||busy||(DateTime.UtcNow-lastCheck).TotalSeconds<2)return;lastCheck=DateTime.UtcNow;bool ok=tunnel!=null&&!tunnel.HasExited;if(ok)ok=await Probe(18766);if(!ok){await Action(Disconnect);SetStatus("연결이 끊겼어요","휴대폰과 USB 케이블을 확인한 뒤 다시 연결해 주세요.","연결 중지",false);}};monitor.Start();
  FormClosing+=async delegate(object sender,FormClosingEventArgs e){if(closing)return;e.Cancel=true;if(busy){statusDetail.Text="작업이 끝난 뒤 창을 닫아 주세요.";return;}closing=true;await Action(Disconnect);Close();};
  SetStatus("연결할 준비가 됐어요",lastInfo,"연결 대기",false);UpdateButtons();
 }
 void SetStatus(string title,string detail,string pill,bool error){statusTitle.Text=title;statusDetail.Text=detail;statusPill.Text=pill;statusPill.ForeColor=error?Color.FromArgb(185,28,28):UiTheme.Accent;}
 void UpdateButtons(){start.Enabled=install.Enabled=restore.Enabled=usageButton.Enabled=!busy;install.Enabled=!busy&&!connected;start.Text=busy?"잠시만 기다려 주세요":connected?"연결 끊기":"인터넷 연결";}
 static string Friendly(string text){if(String.IsNullOrWhiteSpace(text))return "작업을 완료했습니다.";if(text.Contains("Success"))return "휴대폰 앱 설치를 완료했습니다.";if(text.StartsWith("중지됨"))return "연결을 종료하고 인터넷 경로를 복구했습니다.";if(text.StartsWith("전체 연결"))return "노트북의 인터넷 연결이 준비됐습니다. 사용 후 ‘연결 끊기’를 누르세요.";if(text.StartsWith("DNS 조회"))return "휴대폰 인터넷 연결을 준비하고 있습니다.";if(text.StartsWith("일반 UDP"))return "일부 통신을 준비하지 못했습니다. 웹 접속을 확인해 주세요.";string line=text.Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0].Trim();return line.Length>120?line.Substring(0,117)+"…":line;}

 void KeepTunnelMessage(string s){if(String.IsNullOrWhiteSpace(s))return;tunnelMessages.Enqueue(s);string old;while(tunnelMessages.Count>100)tunnelMessages.TryDequeue(out old);}
 void Say(string text){lastInfo=Friendly(text);statusDetail.Text=lastInfo;}
 async Task Action(Func<Task> operation){if(busy)return;busy=true;UpdateButtons();SetStatus("처리 중입니다",lastInfo,"잠시만 기다려 주세요",false);string failure=null;try{await operation();}catch(Exception e){failure=e.Message;}if(failure!=null){try{await Disconnect();}catch(Exception e){failure+="\n"+e.Message;}}busy=false;UpdateButtons();if(failure!=null){SetStatus("연결하지 못했어요",Friendly(failure),"확인이 필요해요",true);}else if(connected){SetStatus("휴대폰에 연결됐어요","노트북의 인터넷 연결이 준비됐습니다. 사용 후 ‘연결 끊기’를 누르세요.","연결됨",false);}else{SetStatus("연결할 준비가 됐어요",lastInfo,"연결 대기",false);}}

 static string Q(string s){return "\""+s.Replace("\"", "")+"\"";}
 Process Begin(string file,string args){var p=new Process();p.StartInfo=new ProcessStartInfo(file,args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};p.Start();return p;}
 async Task<string> Run(string file,string args,int seconds){using(var p=Begin(file,args)){Task<string> stdout=p.StandardOutput.ReadToEndAsync(),stderr=p.StandardError.ReadToEndAsync();Task wait=Task.Run(()=>p.WaitForExit());if(await Task.WhenAny(wait,Task.Delay(seconds*1000))!=wait){try{p.Kill();}catch{}throw new Exception("작업 시간이 초과되었습니다. USB 연결과 휴대폰 허용 창을 확인하세요.");}await wait;string text=await stdout;string error=await stderr;if(p.ExitCode!=0)throw new Exception((text+"\n"+error).Trim());return text;}}
 string AdbArgs(string command){return "-P 5038 "+(serial==null?"":"-s "+Q(serial)+" ")+command;}
 Task<string> Adb(string command){return Run(Path.Combine(root,"platform-tools\\adb.exe"),AdbArgs(command),60);}
 async Task Device(){serial=null;string list=await Adb("devices");string found=null;foreach(string line in list.Split('\n')){string[] p=line.Trim().Split(new char[]{'\t',' '},StringSplitOptions.RemoveEmptyEntries);if(p.Length==2&&p[1]=="device"){if(found!=null)throw new Exception("USB 디버깅 기기를 한 대만 연결해 주세요.");found=p[0];}}if(found==null)throw new Exception("허용된 휴대폰이 없습니다. 데이터 전송용 USB 케이블, USB 디버깅과 휴대폰의 허용 창을 확인하세요.");serial=found;}
 async Task Install(){await Device();Say("휴대폰 앱 설치 중…");await Adb("install -r "+Q(Path.Combine(root,"USBDataBridge.apk")));Say("휴대폰 앱 설치를 완료했습니다.");await Adb("shell am start -n local.usbbridge/.MainActivity");Say("휴대폰에서 모바일 데이터를 켜고 앱의 시작 버튼을 눌러 주세요.");}
 Task<string> Network(string mode){return Run("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Q(Path.Combine(root,"network.ps1"))+" -Mode "+mode,45);}
 async Task Connect(){await Device();await Network("Restore");await Adb("forward tcp:18766 tcp:1080");await Adb("forward tcp:18767 tcp:1081");await Adb("shell am start -n local.usbbridge/.MainActivity --ez autoStart true");Say("휴대폰의 모바일 연결을 기다립니다…");bool ok=false;for(int i=0;i<20;i++){if(await Probe(18766)){ok=true;break;}await Task.Delay(500);}if(!ok)throw new Exception("휴대폰 앱을 열고 시작 버튼을 누르세요. 모바일 데이터가 켜져 있어야 합니다.");
  try{await UdpProbe();}catch(Exception e){Say("일반 UDP 준비 검사 실패: "+e.Message+" · DNS는 HTTPS 통로를 사용합니다.");}bridge=new SocksBridge(0,18766,18767,KeepTunnelMessage);bridge.Start();
  tunnel=Begin(Path.Combine(root,"tun2socks.exe"),"--device USBDataBridge2 --proxy socks5://127.0.0.1:"+bridge.Port+" --mtu 1400 --loglevel warn");
  // Drain logs continuously so the child never blocks on a filled output pipe.
  tunnelMessages=new ConcurrentQueue<string>();tunnel.OutputDataReceived+=delegate(object s,DataReceivedEventArgs e){KeepTunnelMessage(e.Data);};tunnel.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){KeepTunnelMessage(e.Data);};tunnel.BeginOutputReadLine();tunnel.BeginErrorReadLine();
  watch=new Process();watch.StartInfo=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Q(Path.Combine(root,"network.ps1"))+" -Mode Watch -ParentId "+Process.GetCurrentProcess().Id+" -TunnelId "+tunnel.Id){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};watch.Start();
  await Network("Connect");if(tunnel.HasExited)throw new Exception("가상 어댑터가 종료되었습니다. 드라이버 또는 보안 프로그램의 차단 여부를 확인하세요.");connected=true;Say("DNS 조회는 휴대폰의 HTTPS 통로로 전달합니다. 일반 UDP와 별도로 처리합니다.");chargeStatus.Text="데이터 차감 구분은 통신사 사용량으로 확인하세요.";Say("전체 연결 준비됨 · 사용 후 연결 끊기를 눌러 주세요.");}
 async Task Disconnect(){connected=false;string failure=null;try{await Network("Restore");}catch(Exception e){failure=e.Message;}if(bridge!=null){bridge.Dispose();bridge=null;}if(tunnel!=null){try{if(!tunnel.HasExited)tunnel.Kill();}catch{}tunnel.Dispose();tunnel=null;}if(watch!=null){if(failure==null){try{if(!watch.HasExited)watch.Kill();}catch{}}watch.Dispose();watch=null;}if(serial!=null){try{await Adb("forward --remove tcp:18766");await Adb("forward --remove tcp:18767");}catch{}}if(failure!=null)throw new Exception(failure);Say("중지됨 · 이 프로그램의 인터넷 경로를 제거했습니다.");}
 static async Task<bool> Probe(int port){using(var c=new TcpClient()){try{Task t=c.ConnectAsync("127.0.0.1",port);if(await Task.WhenAny(t,Task.Delay(1500))!=t)return false;await t;var s=c.GetStream();await s.WriteAsync(new byte[]{5,1,0},0,3);Task<byte[]> r=SocksBridge.Read(s,2);if(await Task.WhenAny(r,Task.Delay(1500))!=r)return false;byte[] b=await r;return b[0]==5&&b[1]==0;}catch{return false;}}}
 static async Task UdpProbe(){using(var c=new TcpClient()){await c.ConnectAsync("127.0.0.1",18767);var s=c.GetStream();await s.WriteAsync(new byte[]{85,68,66,50},0,4);var t=SocksBridge.Read(s,4);if(await Task.WhenAny(t,Task.Delay(3000))!=t)throw new Exception("휴대폰 UDP 연결 실패 · 동봉된 새 APK를 설치하세요.");byte[] b=await t;if(b[0]!=85||b[1]!=68||b[2]!=66||b[3]!=50)throw new Exception("휴대폰 UDP 응답이 올바르지 않습니다.");}}
}
