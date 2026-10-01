package local.usbbridge;
import android.app.*;
import android.os.*;
import android.content.*;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.widget.*;
public class MainActivity extends Activity {
 private final Handler handler=new Handler();
 private TextView status;
 private final int ink=Color.rgb(24,39,61),muted=Color.rgb(100,116,139),blue=Color.rgb(37,99,235);
 private final Runnable refresh=new Runnable(){public void run(){status.setText(BridgeService.status);handler.postDelayed(this,1000);}};
 private int dp(int n){return (int)(n*getResources().getDisplayMetrics().density+0.5f);}
 private TextView text(String value,int size,int color,boolean bold){TextView t=new TextView(this);t.setText(value);t.setTextSize(size);t.setTextColor(color);if(bold)t.setTypeface(null,Typeface.BOLD);return t;}
 private void gap(LinearLayout box,int height){box.addView(new TextView(this),new LinearLayout.LayoutParams(1,dp(height)));}
 private GradientDrawable background(int color){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(14));return d;}
 private Button button(String title,boolean primary){Button b=new Button(this);b.setText(title);b.setTextSize(16);b.setTextColor(primary?Color.WHITE:ink);b.setAllCaps(false);b.setBackground(background(primary?blue:Color.rgb(235,240,247)));return b;}
 public void onCreate(Bundle saved){super.onCreate(saved);
  getWindow().setStatusBarColor(Color.rgb(244,247,251));getWindow().getDecorView().setSystemUiVisibility(android.view.View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR);
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setBackgroundColor(Color.rgb(244,247,251));
  LinearLayout box=new LinearLayout(this);box.setOrientation(1);box.setPadding(dp(24),dp(28),dp(24),dp(24));scroll.addView(box);
  box.addView(text("USB DATA BRIDGE",12,blue,true));gap(box,14);box.addView(text("휴대폰을 연결하세요",27,ink,true));gap(box,10);box.addView(text("모바일 데이터를 노트북으로 전달합니다.",15,muted,false));gap(box,26);
  LinearLayout card=new LinearLayout(this);card.setOrientation(1);card.setPadding(dp(20),dp(22),dp(20),dp(22));card.setBackground(background(Color.WHITE));box.addView(card);
  card.addView(text("연결 상태",16,ink,true));gap(card,12);status=text(BridgeService.status,18,blue,true);card.addView(status);gap(card,22);
  Button start=button("연결 준비",true);card.addView(start,new LinearLayout.LayoutParams(-1,dp(52)));start.setOnClickListener(v->startBridge());gap(card,12);
  Button stop=button("연결 종료",false);card.addView(stop,new LinearLayout.LayoutParams(-1,dp(52)));stop.setOnClickListener(v->stopService(new Intent(this,BridgeService.class)));
  gap(box,26);box.addView(text("이렇게 사용하세요",17,ink,true));gap(box,12);
  TextView steps=text("1. 모바일 데이터를 켜 주세요.\n2. USB 케이블로 노트북에 연결하세요.\n3. USB 디버깅을 허용하세요.\n4. 노트북에서 ‘인터넷 연결’을 누르세요.",15,muted,false);steps.setLineSpacing(dp(6),1);box.addView(steps);gap(box,24);
  box.addView(text("기본 USB 테더링과 핫스팟은 꺼 주세요.\n데이터 차감 구분은 통신사 사용량으로 확인하세요.",13,muted,false));setContentView(scroll);
  if(Build.VERSION.SDK_INT>=33&&checkSelfPermission("android.permission.POST_NOTIFICATIONS")!=0)requestPermissions(new String[]{"android.permission.POST_NOTIFICATIONS"},1);
  if(getIntent().getBooleanExtra("autoStart",false))startBridge();
 }
 protected void onNewIntent(Intent i){super.onNewIntent(i);setIntent(i);if(i.getBooleanExtra("autoStart",false))startBridge();}
 private void startBridge(){startForegroundService(new Intent(this,BridgeService.class));}
 public void onResume(){super.onResume();handler.post(refresh);}
 public void onPause(){handler.removeCallbacks(refresh);super.onPause();}
}
