using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

// Identify Wintun via its own API. InterfaceDescription is not a driver identity.
public static class USBDataBridgeAdapterCheck {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
 static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true,SetLastError=true)]
 static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Unicode,SetLastError=true)]
 delegate IntPtr Open([MarshalAs(UnmanagedType.LPWStr)]string name);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate void Close(IntPtr adapter);
 public static bool IsWintun(string directory,string name) {
  string path=Path.GetFullPath(Path.Combine(directory,"wintun.dll"));
  IntPtr module=LoadLibraryEx(path,IntPtr.Zero,0x100|0x1000);
  if(module==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot load bundled wintun.dll");
  IntPtr adapter=IntPtr.Zero;Close close=null;
  try {
   IntPtr op=GetProcAddress(module,"WintunOpenAdapter"),cl=GetProcAddress(module,"WintunCloseAdapter");
   if(op==IntPtr.Zero||cl==IntPtr.Zero)throw new Exception("Missing Wintun adapter API");
   var open=(Open)Marshal.GetDelegateForFunctionPointer(op,typeof(Open));
   close=(Close)Marshal.GetDelegateForFunctionPointer(cl,typeof(Close));
   // Open existing adapter only: no installation, creation or route changes.
   adapter=open(name);return adapter!=IntPtr.Zero;
  } finally {if(adapter!=IntPtr.Zero&&close!=null)close(adapter);FreeLibrary(module);}
 }
}
