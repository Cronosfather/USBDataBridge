using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
class Recovery {
 [STAThread]static void Main(){try{string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"network.ps1");var si=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+path+"\" -Mode Restore"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};using(var p=Process.Start(si)){string error=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(error);}MessageBox.Show("USBDataBridge의 인터넷 경로를 제거했습니다.","인터넷 경로 복구");}catch(Exception e){MessageBox.Show(e.Message,"복구 오류");}}
}
