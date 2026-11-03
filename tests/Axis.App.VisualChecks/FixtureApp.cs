using System.Windows;
using System.Windows.Markup;
using System.IO;
using System.Xml.Linq;
// WPF queues OnStartup even when a harness pumps the Dispatcher without Application.Run.
// Keep production resources, but never implicitly create a product window/collectors.
sealed class FixtureApp : Application {
 public void InitializeComponent(){
  // Load the exact product styles as a dictionary, without constructing its Application root.
  var app=XDocument.Load(Path.Combine(Environment.CurrentDirectory,"src","Axis.App","App.xaml")).Root!;
  XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
  var dictionary=new XElement(ns+"ResourceDictionary",app.Attributes().Where(a=>a.IsNamespaceDeclaration),app.Element(ns+"Application.Resources")!.Elements());
  Resources=(ResourceDictionary)XamlReader.Parse(dictionary.ToString());
 }
}
