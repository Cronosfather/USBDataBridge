package local.usbbridge;

import java.io.*;
import java.net.*;
import java.util.*;
import java.util.concurrent.*;

/** UDP datagrams framed over the TCP-only ADB transport. */
public final class UdpRelay implements Closeable {
    public interface NetworkAccess {
        DatagramSocket open() throws IOException;
        InetAddress[] resolve(String host) throws IOException;
    }
    private final NetworkAccess access;
    private final ServerSocket listener;
    private final Set<Socket> clients=ConcurrentHashMap.newKeySet();
    private final Set<DatagramSocket> datagrams=ConcurrentHashMap.newKeySet();
    private final Semaphore slots=new Semaphore(64);
    private final ExecutorService workers=Executors.newCachedThreadPool();
    private volatile boolean closed;
    public UdpRelay(int port,NetworkAccess access) throws IOException {
        this.access=access;listener=new ServerSocket();listener.setReuseAddress(true);
        listener.bind(new InetSocketAddress("127.0.0.1",port),64);
    }
    public int port(){return listener.getLocalPort();}
    public void serve() throws IOException {
        while(!closed) {
            Socket s=listener.accept();
            if(!slots.tryAcquire()){s.close();continue;}
            clients.add(s);
            try{workers.execute(()->session(s));}catch(RejectedExecutionException e){clients.remove(s);s.close();slots.release();}
        }
    }
    private void session(Socket s) {
        DatagramSocket udp=null;
        try {
            s.setSoTimeout(15000);s.setTcpNoDelay(true);
            DataInputStream in=new DataInputStream(s.getInputStream());
            DataOutputStream out=new DataOutputStream(s.getOutputStream());
            if(in.readInt()!=0x55444232)return;
            udp=access.open();datagrams.add(udp);
            if(closed)return;
            udp.setReceiveBufferSize(262144);
            out.writeInt(0x55444232);out.flush();s.setSoTimeout(0);
            final DatagramSocket socket=udp;
            final Map<String,Boolean> peers=Collections.synchronizedMap(new LinkedHashMap<String,Boolean>(64,0.75f,true){
                protected boolean removeEldestEntry(Map.Entry<String,Boolean> e){return size()>2048;}
            });
            workers.execute(()->{
                byte[] buffer=new byte[65507];
                try {
                    while(!closed) {
                        DatagramPacket reply=new DatagramPacket(buffer,buffer.length);socket.receive(reply);
                        String key=reply.getAddress().getHostAddress()+":"+reply.getPort();
                        if(!peers.containsKey(key))continue;
                        byte[] ip=reply.getAddress().getAddress();
                        ByteArrayOutputStream bytes=new ByteArrayOutputStream();DataOutputStream packet=new DataOutputStream(bytes);
                        packet.writeShort(0);packet.writeByte(0);packet.writeByte(ip.length==4?1:4);packet.write(ip);packet.writeShort(reply.getPort());
                        packet.write(reply.getData(),reply.getOffset(),reply.getLength());byte[] result=bytes.toByteArray();
                        out.writeInt(result.length);out.write(result);out.flush();
                    }
                }catch(IOException ignored){try{s.close();}catch(IOException ignored2){}}
            });
            while(!closed) {
                int length=in.readInt();if(length<7||length>65535)throw new IOException("Invalid frame length");
                byte[] bytes=new byte[length];in.readFully(bytes);
                DataInputStream packet=new DataInputStream(new ByteArrayInputStream(bytes));
                if(packet.readUnsignedShort()!=0||packet.readUnsignedByte()!=0)continue;
                int type=packet.readUnsignedByte();InetAddress ip;
                try {
                    if(type==1||type==4){byte[] address=new byte[type==1?4:16];packet.readFully(address);ip=InetAddress.getByAddress(address);}
                    else if(type==3){int n=packet.readUnsignedByte();if(n==0)continue;byte[] name=new byte[n];packet.readFully(name);InetAddress[] addresses=access.resolve(new String(name,java.nio.charset.StandardCharsets.US_ASCII));if(addresses.length==0)continue;ip=addresses[0];}
                    else continue;
                    int port=packet.readUnsignedShort();if(port==0)continue;
                    int offset=bytes.length-packet.available();if(packet.available()>65507)continue;
                    peers.put(ip.getHostAddress()+":"+port,true);
                    socket.send(new DatagramPacket(bytes,offset,bytes.length-offset,ip,port));
                }catch(IOException ignored){/* An unreachable UDP destination must not kill other flows. */}
            }
        }catch(IOException|RejectedExecutionException ignored) {
        }finally {
            if(udp!=null){datagrams.remove(udp);udp.close();}
            clients.remove(s);try{s.close();}catch(IOException ignored){}slots.release();
        }
    }
    public void close(){closed=true;try{listener.close();}catch(IOException ignored){}for(Socket s:clients)try{s.close();}catch(IOException ignored){}for(DatagramSocket s:datagrams)s.close();workers.shutdownNow();}
}
