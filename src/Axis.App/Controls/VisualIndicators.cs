using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Axis.Core.Presentation;
namespace Axis.App.Controls;
public sealed class QuotaDial : FrameworkElement {
 double? remaining;bool fresh;
 public QuotaDial(){Width=124;Height=124;SnapsToDevicePixels=false;}
 public void SetReading(double? value,bool current){if(remaining==value&&fresh==current)return;remaining=value;fresh=current;InvalidateVisual();}
 internal static Brush Ink(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
 internal static Geometry Arc(Point center,double radius,double sweep){
  double end=(sweep-90)*Math.PI/180;
  var geometry=new StreamGeometry();
  using(var c=geometry.Open()){c.BeginFigure(new(center.X,center.Y-radius),false,false);c.ArcTo(new(center.X+radius*Math.Cos(end),center.Y+radius*Math.Sin(end)),new(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false);}
  geometry.Freeze();return geometry;
 }
 internal static void Label(DrawingContext dc,string value,Point center,double size,Brush ink,double dpi,FontWeight? weight=null){
  var text=new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,weight??FontWeights.SemiBold,FontStretches.Normal),size,ink,dpi);
  dc.DrawText(text,new(center.X-text.Width/2,center.Y-text.Height/2));
 }
 protected override void OnRender(DrawingContext dc){
  var center=new Point(ActualWidth/2,ActualHeight/2);double radius=Math.Min(ActualWidth,ActualHeight)*.40;
  var track=new Pen(Ink("#2D3B50"),8);dc.DrawEllipse(Ink("#111A29"),null,center,radius-5,radius-5);dc.DrawEllipse(null,track,center,radius,radius);
  var color=Ink(fresh?DisplayRules.QuotaHex(remaining):"#8BA0B1");
  if(remaining is {} value&&double.IsFinite(value)){
   value=Math.Clamp(value,0,100);var pen=new Pen(color,8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
   if(value>=100)dc.DrawEllipse(null,pen,center,radius,radius);else if(value>0)dc.DrawGeometry(null,pen,Arc(center,radius,Math.Max(.1,value*3.6)));
  }
  double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
  string digits=remaining is {} n&&double.IsFinite(n)?Math.Round(Math.Clamp(n,0,100),MidpointRounding.AwayFromZero).ToString("0")+" %":"—";
  Label(dc,digits,new(center.X,center.Y-3),25,color,dpi,FontWeights.Bold);
  Label(dc,remaining is null?"indisponible":fresh?"restant":"ancien",new(center.X,center.Y+23),11,Ink("#A4B5CB"),dpi,FontWeights.Normal);
 }
}
public sealed class MissionGlyph : FrameworkElement {
 readonly RotateTransform rotation=new();string state="NON VÉRIFIÉ";
 public MissionGlyph(){Width=30;Height=30;RenderTransform=rotation;RenderTransformOrigin=new(.5,.5);IsVisibleChanged+=(_,_)=>Animate();Unloaded+=(_,_)=>rotation.BeginAnimation(RotateTransform.AngleProperty,null);}
 public void SetState(string value){
  if(value==state)return;string previous=state;state=value;Animate();InvalidateVisual();
  if(previous=="EN COURS"&&value=="TERMINÉ")BeginAnimation(OpacityProperty,new DoubleAnimation(.45,1,TimeSpan.FromMilliseconds(450)));
 }
 void Animate(){
  rotation.BeginAnimation(RotateTransform.AngleProperty,null);rotation.Angle=0;
  if(state=="EN COURS"&&IsVisible)rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromSeconds(2.2)){RepeatBehavior=RepeatBehavior.Forever});
 }
 protected override void OnRender(DrawingContext dc){
  var color=QuotaDial.Ink(state switch{"EN COURS"=>"#A477FF","TERMINÉ"=>"#51D7AF","ÉCHEC"=>"#EE4B55","INTERROMPU"=>"#F28C38",_=>"#8BA0B1"});
  var pen=new Pen(color,2.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
  dc.DrawEllipse(null,new Pen(QuotaDial.Ink("#344157"),2),new(15,15),11,11);
  if(state=="EN COURS")dc.DrawGeometry(null,pen,QuotaDial.Arc(new(15,15),11,260));
  else if(state=="TERMINÉ"){dc.DrawLine(pen,new(8,15),new(13,20));dc.DrawLine(pen,new(13,20),new(22,10));}
  else if(state=="ÉCHEC"){dc.DrawLine(pen,new(10,10),new(20,20));dc.DrawLine(pen,new(20,10),new(10,20));}
  else if(state=="INTERROMPU"){dc.DrawLine(pen,new(12,10),new(12,20));dc.DrawLine(pen,new(18,10),new(18,20));}
  else dc.DrawEllipse(color,null,new(15,15),2,2);
 }
}
