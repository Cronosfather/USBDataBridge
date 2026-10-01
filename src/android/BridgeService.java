package local.usbbridge;

import android.app.*;
import android.os.*;
import android.net.*;
import android.content.*;
import java.io.*;
import java.net.*;

public class BridgeService extends Service {
    public static volatile String status = "중지됨";
    private ConnectivityManager cm;
    private ConnectivityManager.NetworkCallback callback;
    private RelayServer relay;
    private UdpRelay udpRelay;
    private PowerManager.WakeLock wake;
    private volatile Network network;
    private volatile boolean ending;
    public void onCreate() {
        super.onCreate();
        NotificationManager nm = getSystemService(NotificationManager.class);
        nm.createNotificationChannel(new NotificationChannel("bridge", "USB 데이터 연결", NotificationManager.IMPORTANCE_LOW));
        PendingIntent open = PendingIntent.getActivity(this, 0, new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE);
        startForeground(1,new Notification.Builder(this,"bridge").setContentTitle("USB 데이터 연결 실행 중")
            .setContentText("종료하려면 앱에서 연결 중지를 누르세요")
            .setSmallIcon(android.R.drawable.stat_sys_upload).setContentIntent(open).setOngoing(true).build());
        wake = getSystemService(PowerManager.class).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "USBBridge:relay");
        wake.acquire();
        cm = getSystemService(ConnectivityManager.class);
        status = "모바일 데이터 연결 대기 중…";
        callback = new ConnectivityManager.NetworkCallback() {
            public void onAvailable(Network n) {
                synchronized(BridgeService.this) {
                    if (ending) return;
                    network = n;
                    if (relay != null) return;
                    try {
                        relay = new RelayServer(1080, (host,port) -> {
                            Network active = network;
                            if (active == null) throw new IOException("No cellular network");
                            IOException last = null;
                            for (InetAddress ip : active.getAllByName(host)) {
                                Socket s = active.getSocketFactory().createSocket();
                                try { s.connect(new InetSocketAddress(ip,port),15000); return s; }
                                catch(IOException e) { last=e; s.close(); }
                            }
                            throw last != null ? last : new UnknownHostException(host);
                        });
                        RelayServer server = relay;
                        new Thread(() -> { try { server.serve(); } catch(IOException ignored) { } },"usb-relay").start();
                        udpRelay = new UdpRelay(1081,new UdpRelay.NetworkAccess(){
                            public DatagramSocket open() throws IOException {
                                Network active=network;if(active==null)throw new IOException("No cellular network");
                                DatagramSocket s=new DatagramSocket(null);
                                try {active.bindSocket(s);s.bind(new InetSocketAddress(0));return s;}
                                catch(IOException e){s.close();throw e;}
                            }
                            public InetAddress[] resolve(String host) throws IOException {
                                Network active=network;if(active==null)throw new IOException("No cellular network");return active.getAllByName(host);
                            }
                        });
                        UdpRelay udpServer=udpRelay;
                        new Thread(()->{try{udpServer.serve();}catch(IOException ignored){}},"usb-udp").start();
                        status = "준비 완료 · TCP/UDP 모바일 연결";
                    } catch(IOException e) { status = "연결 오류: " + e.getMessage(); stopSelf(); }
                }
            }
            public void onLost(Network n) {
                synchronized(BridgeService.this) {
                    if(n.equals(network)) { network=null; if(relay!=null) { relay.close(); relay=null; } if(udpRelay!=null){udpRelay.close();udpRelay=null;} status="모바일 데이터 연결이 끊겼습니다"; }
                }
            }
            public void onUnavailable() { status="모바일 데이터를 켠 뒤 다시 시작하세요"; stopSelf(); }
        };
        try { cm.requestNetwork(new NetworkRequest.Builder().addTransportType(NetworkCapabilities.TRANSPORT_CELLULAR)
                .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET).build(),callback,20000); }
        catch (RuntimeException e) { status="모바일 데이터 요청 실패: " + e.getMessage(); stopSelf(); }
    }
    public int onStartCommand(Intent i,int flags,int id) { return START_NOT_STICKY; }
    public IBinder onBind(Intent i) { return null; }
    public synchronized void onDestroy() {
        ending=true; network=null;
        if (callback!=null && cm!=null) try { cm.unregisterNetworkCallback(callback); } catch(RuntimeException ignored) { }
        if(relay!=null) { relay.close(); relay=null; }
        if(udpRelay!=null){udpRelay.close();udpRelay=null;}
        if(wake!=null && wake.isHeld()) wake.release();
        if(status.startsWith("준비") || status.contains("대기")) status="중지됨";
        stopForeground(STOP_FOREGROUND_REMOVE); super.onDestroy();
    }
}
