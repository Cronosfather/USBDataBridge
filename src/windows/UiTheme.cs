using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class UiTheme {
 public static readonly Color Background=Color.FromArgb(244,247,251),Ink=Color.FromArgb(24,39,61),Muted=Color.FromArgb(100,116,139),Accent=Color.FromArgb(37,99,235),Border=Color.FromArgb(225,232,241);
 public static Font Font(float size,FontStyle style=FontStyle.Regular){return new Font("Malgun Gothic",size,style);}
 public static Label Label(string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular){return new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=Font(size,style),ForeColor=color,BackColor=Color.Transparent};}
 public static void Button(Button b,string text,int x,int y,int w,int h,bool primary){b.Text=text;b.SetBounds(x,y,w,h);b.Font=Font(10,FontStyle.Bold);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=primary?0:1;b.FlatAppearance.BorderColor=Border;b.BackColor=primary?Accent:Color.White;b.ForeColor=primary?Color.White:Ink;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;}
 public static GraphicsPath Round(Rectangle bounds,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(bounds.Left,bounds.Top,d,d,180,90);p.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);p.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);p.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
class UiCard : Panel {
 public UiCard(){DoubleBuffered=true;BackColor=UiTheme.Background;}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=UiTheme.Round(new Rectangle(0,0,Width-1,Height-1),14)){e.Graphics.FillPath(Brushes.White,p);using(var pen=new Pen(UiTheme.Border))e.Graphics.DrawPath(pen,p);}}
}
