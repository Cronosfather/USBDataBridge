using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

public static class UsageObservation {
 public static string Compare(decimal? general,decimal shared,string generalUnit,string sharedUnit){
  if(shared<0||(general.HasValue&&general.Value<0))return "KT 차감 구분: 비교 불가 · 조회값 감소/월 변경/입력값을 확인하세요.";
  string note=" 갱신 지연과 다른 기기의 사용이 섞일 수 있어 이 연결의 차감 구분을 확정하지 않습니다.";
  if(shared>0)return "KT 조회값: 테더링/공유 사용량 +"+shared.ToString("0.###")+" "+sharedUnit+" 관찰됨."+note;
  if(general.HasValue&&general.Value>0)return "KT 조회값: 일반 사용량 +"+general.Value.ToString("0.###")+" "+generalUnit+", 공유 사용량 변화 없음."+note;
  return "KT 조회값: 공유 사용량 증가가 관찰되지 않았습니다. 사용량 반영 후 다시 확인하세요. 일반 데이터로 차감됐다는 확정은 아닙니다.";
 }
}

class UsageCheckForm : Form {
 readonly TextBox[] before={new TextBox(),new TextBox()},after={new TextBox(),new TextBox()};
 readonly ComboBox[] mode={new ComboBox(),new ComboBox()},unit={new ComboBox(),new ComboBox()};
 readonly Label result=new Label();public event Action<string> ResultChanged;
 public UsageCheckForm(){Text="데이터 사용량 확인";ClientSize=new Size(740,410);Font=UiTheme.Font(10);BackColor=UiTheme.Background;ForeColor=UiTheme.Ink;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterParent;
  Controls.Add(new Label{Text="마이케이티에서 연결 전·후의 조회값을 확인해 입력하세요. 자동 조회 기능은 아닙니다.\n일반 데이터가 무제한이라 수치가 없다면 첫 줄은 비워두세요.\n공유 사용량은 해당 기간에 차감되는 Y덤/기본 제공량을 함께 확인하세요.",Location=new Point(18,16),Size=new Size(704,78)});
  string[] headings={"항목","연결 전","연결 후","조회값 종류","단위"};int[] xs={18,265,385,505,635};int[] ws={240,110,110,120,85};for(int i=0;i<5;i++)Controls.Add(new Label{Text=headings[i],Location=new Point(xs[i],108),Size=new Size(ws[i],25)});
  for(int i=0;i<2;i++){int y=138+i*45;Controls.Add(new Label{Text=i==0?"일반 데이터 (선택)":"테더링/공유 데이터",Location=new Point(18,y+3),Size=new Size(240,30)});before[i].SetBounds(265,y,110,30);after[i].SetBounds(385,y,110,30);Controls.Add(before[i]);Controls.Add(after[i]);mode[i].DropDownStyle=ComboBoxStyle.DropDownList;mode[i].Items.AddRange(new object[]{"누적 사용량","남은 제공량"});mode[i].SelectedIndex=0;mode[i].SetBounds(505,y,120,30);Controls.Add(mode[i]);unit[i].DropDownStyle=ComboBoxStyle.DropDownList;unit[i].Items.AddRange(new object[]{"MB","GB"});unit[i].SelectedIndex=0;unit[i].SetBounds(635,y,85,30);Controls.Add(unit[i]);}
  var compare=new Button{Text="사용량 비교",Location=new Point(18,237),Size=new Size(145,38)};compare.Click+=delegate{Compare();};Controls.Add(compare);
  result.SetBounds(18,290,704,102);result.Text="KT 차감 구분: 미확인\n휴대폰 앱의 모바일 연결 상태만으로 KT 차감 항목을 판별할 수 없습니다.";Controls.Add(result);
 }
 decimal? Delta(int row,bool required){string b=before[row].Text.Trim(),a=after[row].Text.Trim();if(!required&&b==""&&a=="")return null;decimal bv,av;if(!Parse(b,out bv)||!Parse(a,out av)||bv<0||av<0)throw new Exception((row==0?"일반":"공유")+" 데이터의 연결 전·후 값을 숫자로 입력하세요. 같은 단위의 조회값을 사용하세요.");return mode[row].SelectedIndex==0?av-bv:bv-av;}
 static bool Parse(string s,out decimal v){return decimal.TryParse(s,NumberStyles.AllowDecimalPoint|NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out v);}
 void Compare(){try{decimal? general=Delta(0,false),shared=Delta(1,true);string text=UsageObservation.Compare(general,shared.Value,unit[0].Text,unit[1].Text);result.Text=text;if(ResultChanged!=null)ResultChanged(text);}catch(Exception e){MessageBox.Show(this,e.Message,"입력값 확인");}}
}
