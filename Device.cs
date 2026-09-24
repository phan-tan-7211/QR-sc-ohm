using System.Globalization;
using System.IO;
using System.IO.Ports;

namespace UC2836Live;
public sealed class Device : IDisposable
{
 readonly SerialPort port;
 public Device(string name, int baud)
 {
  port = new SerialPort(name,baud,Parity.None,8,StopBits.One) { NewLine="\n",ReadTimeout=1800,WriteTimeout=1800,Handshake=Handshake.None };
  try { port.Open(); port.DiscardInBuffer(); } catch { port.Dispose(); throw; }
 }
 public string Query(string command)
 {
  port.WriteLine(command);
  // Some firmware emits LF CR: Trim removes the CR left before the next line.
  string line=port.ReadLine().Trim();
  if(line.Length==0) line=port.ReadLine().Trim();
  return line;
 }
 public void Dispose() { port.Dispose(); }
 public void Write(string command) => port.WriteLine(command);
 public void RequestMeasurement()=>port.WriteLine("FETC?");
 public void TriggerMeasurement()=>port.WriteLine("TRIG");
 public string ReadMeasurement()=>port.ReadLine().Trim();
 public void FlushStaleResult()
 {
  port.DiscardInBuffer();
  port.WriteLine("FETC?");
  var prev=port.ReadTimeout;port.ReadTimeout=350;
  try{port.ReadLine();}catch{}finally{port.ReadTimeout=prev;}
  // Một số firmware trả kết quả trễ; xả thêm các dòng còn nằm trong bộ đệm.
  port.DiscardInBuffer();
 }
 public static string Probe()
 {
  using var d=new Device("COM3",9600);
  var id=d.Query("*IDN?");
  if(!id.Contains("UC2836CX",StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected identity: "+id);
  var mode=d.Query(":TRAN:MODE?");
  var dual=d.Query(":TRAN:DF?")=="1";
  var dc=d.Query(":TRAN:DCR?")=="1";
  var watch=System.Diagnostics.Stopwatch.StartNew();
  var lines=new List<string> { id,$"Mode={mode}, Dual={dual}, DCR={dc}" };
  for(int i=0;i<15;i++) {var m=Measurement.Parse(d.Query("FETC?"),dual,dc); lines.Add(m.Raw);}
  lines.Add($"PASS: 15 complete responses parsed in {watch.ElapsedMilliseconds} ms. No settings changed.");
  return string.Join(Environment.NewLine,lines);
 }
}
public sealed record Measurement(DateTime Time,double LA,double LB,double C,string Compare,string Polarity,string Wire,string RA,string RB,string Second,string Raw)
{
 public string QrCode {get;init;}="";
 public string PassFail {get;init;}="CHƯA PHÂN LOẠI";
 public bool IsLcr {get;init;}
 public string MeasureStatus {get;init;}="0";
 public string Function {get;init;}="";
 public string DisplayA=>Engineering.Format(LA,IsLcr?Engineering.Units(Function).unitA:"H");
 public string DisplayB=>Engineering.Format(LB,IsLcr?Engineering.Units(Function).unitB:"H");
 public string DisplayC=>IsLcr?"—":CText;
 public string DisplayRA=>double.TryParse(RA,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?Engineering.Format(n,"Ω"):RA;
 public string DisplayRB=>double.TryParse(RB,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?Engineering.Format(n,"Ω"):RB;
 public string TimeText=>Time.ToString("HH:mm:ss.fff");
 public string LAText=>LA.ToString("G7",CultureInfo.InvariantCulture);
 public string LBText=>LB.ToString("G7",CultureInfo.InvariantCulture);
 public string CText=>C.ToString("G7",CultureInfo.InvariantCulture);
 public static Measurement Parse(string raw,bool dual,bool dc)
 {
  var v=raw.Split(',').Select(s=>s.Trim()).ToArray();
  if(v.Length is 3 or 4)
  {
   if(!double.TryParse(v[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var primary)||!double.IsFinite(primary)||
      !double.TryParse(v[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var secondary)||!double.IsFinite(secondary)||
      !int.TryParse(v[2],out var status))throw new FormatException("Phản hồi LCR không hợp lệ: "+raw);
   var bin=v.Length==4?v[3]:"—";
   return new(DateTime.Now,primary,secondary,0,bin,"—","—","—","—","Chế độ LCR · thông số chính / phụ",raw){IsLcr=true,MeasureStatus=status.ToString()};
  }
  int expected=(dual?13:6)+(dc?3:0);
  if(v.Length!=expected) throw new FormatException($"Cần {expected} trường, nhận {v.Length}. Kiểm tra chế độ máy. Dữ liệu: {raw}");
  double Number(int i) { if(!double.TryParse(v[i],NumberStyles.Float,CultureInfo.InvariantCulture,out var n)||!double.IsFinite(n)) throw new FormatException("Giá trị không hợp lệ: "+v[i]); return n; }
  var la=Number(0);var lb=Number(1);var c=Number(2);
  if(dual) foreach(int i in new[]{3,5,6,7,8}) Number(i);
  int tail=dual?10:4;
  if(dc) {Number(tail);Number(tail+1);}
  int polarity=tail+(dc?3:0);
  return new(DateTime.Now,la,lb,c,v[dual?4:3],dual?$"{v[polarity]} / {v[polarity+1]}":v[polarity],v[^1],dc?v[tail]:"—",dc?v[tail+1]:"—",dual?$"LA2 {v[5]} · LB2 {v[6]} · N2 {v[7]} · Δ2 {v[8]} · mã {v[9]}":"Tắt đo hai tần số",raw);
 }
 public static string SelfTest()
 {
  Engineering.Test();
  var a=Parse("-3.73377E+00,-3.77777E+00,4.40004E-02,1,+,1",false,false);
  if(a.LA!=-3.73377||a.Wire!="1"||a.Polarity!="+") throw new Exception("Single parse failed");
  var b=Parse("1,2,3,1,4,5,2,-,2",false,true);
  if(b.RA!="4"||b.RB!="5"||b.Polarity!="-") throw new Exception("DCR parse failed");
  var c=Parse("1,2,3,4,1,5,6,7,8,2,+,-,1",true,false);
  if(c.Polarity!="+ / -"||c.Compare!="1") throw new Exception("Dual parse failed");
  var d=Parse("1,2,3,4,1,5,6,7,8,2,9,10,1,+,-,2",true,true);
  if(d.RA!="9"||d.RB!="10"||d.Wire!="2") throw new Exception("Dual DCR parse failed");
  var lcr=Parse("3.0785E-3,3.6367E-1,+0,+1",false,false);
  if(!lcr.IsLcr||lcr.LA!=0.0030785||lcr.LB!=0.36367||lcr.Compare!="+1")throw new Exception("LCR parse failed");
  if(!Parse("1,2,0",false,false).IsLcr)throw new Exception("LCR without bin failed");
  foreach(var bad in new[]{"1,2","NaN,2,0,1","NaN,2,3,1,+,1","abc,2,3,1,+,1"})
  {bool rejected=false;try {Parse(bad,false,false);} catch(FormatException){rejected=true;} if(!rejected)throw new Exception("Invalid packet accepted");}
  return "PASS: balance, DCR, dual, dual+DCR, LCR with/without bin and malformed response tests.";
 }
}
