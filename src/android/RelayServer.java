package local.usbbridge;

import java.io.*;
import java.net.*;
import java.util.*;
import java.util.concurrent.*;

/** Loopback-only SOCKS5 TCP relay. DNS and outbound sockets belong to Connector. */
public final class RelayServer implements Closeable {
    public interface Connector { Socket connect(String host, int port) throws IOException; }
    private final Connector connector;
    private final ServerSocket listener;
    private final Set<Socket> sockets = ConcurrentHashMap.newKeySet();
    private final Semaphore capacity = new Semaphore(128);
    private final ExecutorService workers = Executors.newCachedThreadPool();
    private volatile boolean closed;

    public RelayServer(int port, Connector connector) throws IOException {
        this.connector = connector;
        listener = new ServerSocket();
        listener.setReuseAddress(true);
        listener.bind(new InetSocketAddress("127.0.0.1", port), 32);
    }
    public int port() { return listener.getLocalPort(); }
    public void serve() throws IOException {
        while (!closed) {
            Socket client = listener.accept();
            if (!capacity.tryAcquire()) { client.close(); continue; }
            sockets.add(client);
            try { workers.execute(() -> handle(client)); }
            catch (RejectedExecutionException e) { sockets.remove(client); client.close(); capacity.release(); }
        }
    }
    private void handle(Socket client) {
        Socket remote = null;
        try {
            client.setSoTimeout(15000);
            DataInputStream in = new DataInputStream(client.getInputStream());
            OutputStream out = client.getOutputStream();
            if (in.readUnsignedByte() != 5) return;
            int count = in.readUnsignedByte();
            boolean noAuth = false;
            for (int i = 0; i < count; i++) if (in.readUnsignedByte() == 0) noAuth = true;
            out.write(new byte[]{5, (byte)(noAuth ? 0 : 255)}); out.flush();
            if (!noAuth) return;
            int version = in.readUnsignedByte(), cmd = in.readUnsignedByte(), reserved = in.readUnsignedByte();
            int type = in.readUnsignedByte();
            if (version != 5 || reserved != 0) { reply(out, 1); return; }
            if (cmd != 1) { reply(out, 7); return; }
            String host;
            if (type == 3) {
                int len = in.readUnsignedByte();
                if (len == 0) { reply(out, 8); return; }
                byte[] b = new byte[len]; in.readFully(b);
                host = new String(b, java.nio.charset.StandardCharsets.US_ASCII);
            } else if (type == 1 || type == 4) {
                byte[] b = new byte[type == 1 ? 4 : 16]; in.readFully(b);
                host = InetAddress.getByAddress(b).getHostAddress();
            } else { reply(out, 8); return; }
            int port = in.readUnsignedShort();
            if (port == 0) { reply(out, 1); return; }
            try { remote = connector.connect(host, port); }
            catch (IOException e) { reply(out, 4); return; }
            sockets.add(remote);
            if (closed) return;
            reply(out, 0);
            client.setSoTimeout(0); remote.setSoTimeout(0);
            client.setTcpNoDelay(true); remote.setTcpNoDelay(true);
            Socket target = remote;
            Future<?> upload = workers.submit(() -> pump(client, target));
            pump(target, client);
            try { upload.get(); } catch (Exception ignored) { }
        } catch (IOException | RejectedExecutionException ignored) {
        } finally {
            closeSocket(client); closeSocket(remote); capacity.release();
        }
    }
    private static void reply(OutputStream out, int code) throws IOException {
        out.write(new byte[]{5, (byte)code, 0, 1, 0, 0, 0, 0, 0, 0}); out.flush();
    }
    private static void pump(Socket from, Socket to) {
        try {
            byte[] buffer = new byte[32768];
            int n;
            InputStream in = from.getInputStream(); OutputStream out = to.getOutputStream();
            while ((n = in.read(buffer)) != -1) out.write(buffer, 0, n);
            to.shutdownOutput();
        } catch (IOException e) {
            try { from.close(); } catch (IOException ignored) { }
            try { to.close(); } catch (IOException ignored) { }
        }
    }
    private void closeSocket(Socket s) {
        if (s != null) { sockets.remove(s); try { s.close(); } catch (IOException ignored) { } }
    }
    public void close() {
        closed = true;
        try { listener.close(); } catch (IOException ignored) { }
        for (Socket s : sockets) closeSocket(s);
        workers.shutdownNow();
    }
}
