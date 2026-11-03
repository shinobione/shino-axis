using AxisLab;

if(args.Length<3) throw new ArgumentException("state-directory relay-schema android-schema [--gui]");
using var engine=new LabEngine(args[0],args[1],args[2]);
engine.RegisterExtension(new ClockModule());
await using var app=LabHttp.Create(args,engine);
if(args.Contains("--gui"))
{
    var gui=new Thread(()=>
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        using var form=new Form{Text="AXIS P1 — TEST — Candidate A",Width=800,Height=500};
        var text=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both};
        form.Controls.Add(text);
        using var timer=new System.Windows.Forms.Timer{Interval=1000};
        timer.Tick+=(_,_)=>text.Text=engine.Observe().ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
        timer.Start();
        form.FormClosed+=(_,_)=>app.Lifetime.StopApplication();
        app.Lifetime.ApplicationStopping.Register(()=>{if(form.IsHandleCreated)form.BeginInvoke(()=>form.Close());});
        System.Windows.Forms.Application.Run(form);
    });
    gui.SetApartmentState(ApartmentState.STA); gui.Start();
}
await app.RunAsync();
