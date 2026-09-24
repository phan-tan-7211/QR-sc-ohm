using System.Globalization;
namespace UC2836Live;
public static class Engineering
{
 public static string Format(double value,string unit)
 {
  if(!double.IsFinite(value)||Math.Abs(value)>=1e30)return "OL";
  if(unit.Length==0)return value.ToString("G7",CultureInfo.InvariantCulture);
  if(unit is "°" or "rad" or "%")return value.ToString("G7",CultureInfo.InvariantCulture)+" "+unit;
  var prefixes=new (double scale,string prefix)[]{(1e9,"G"),(1e6,"M"),(1e3,"k"),(1,""),(1e-3,"m"),(1e-6,"µ"),(1e-9,"n"),(1e-12,"p")};
  var item=value==0?prefixes[3]:prefixes.FirstOrDefault(x=>Math.Abs(value)>=x.scale,prefixes[^1]);
  return (value/item.scale).ToString("G7",CultureInfo.InvariantCulture)+" "+item.prefix+unit;
 }
 public static (string nameA,string unitA,string nameB,string unitB) Units(string function)=>function.ToUpperInvariant() switch
 {
  "LSDCR"=>("Ls","H","DCR","Ω"), "DCR"=>("DCR","Ω","Phụ",""),
  "LSRS"=>("Ls","H","Rs","Ω"),"LPRP"=>("Lp","H","Rp","Ω"),
  "LSD"=>("Ls","H","D",""),"LSQ"=>("Ls","H","Q",""),
  "LPD"=>("Lp","H","D",""),"LPQ"=>("Lp","H","Q",""),"LPG"=>("Lp","H","G","S"),
  "CSD"=>("Cs","F","D",""),"CSQ"=>("Cs","F","Q",""),"CSRS"=>("Cs","F","Rs","Ω"),
  "CPD"=>("Cp","F","D",""),"CPQ"=>("Cp","F","Q",""),"CPG"=>("Cp","F","G","S"),"CPRP"=>("Cp","F","Rp","Ω"),
  "RX"=>("R","Ω","X","Ω"),"ZTD"=>("Z","Ω","θ","°"),"ZTR"=>("Z","Ω","θ","rad"),
  "GB"=>("G","S","B","S"),"YTD"=>("Y","S","θ","°"),"YTR"=>("Y","S","θ","rad"),
  _=>("Chính","","Phụ","")
 };
 public static void Test()
 {
  foreach(var (v,u,want) in new[]{(0.002659,"H","2.659 mH"),(0.000002659,"H","2.659 µH"),(0.363,"Ω","363 mΩ"),(1200d,"Ω","1.2 kΩ"),(0d,"H","0 H"),(9.9e37,"H","OL"),(-0.002,"H","-2 mH")})
   if(Format(v,u)!=want)throw new Exception("Unit conversion: "+Format(v,u)+" != "+want);
 }
}
