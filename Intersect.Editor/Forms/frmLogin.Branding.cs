using Intersect.Framework.Core;
using Intersect.Framework.Core.QualityOfLife;
namespace Intersect.Editor.Forms;
public partial class FrmLogin
{
    private void ModernizeLogin()
    {
        string T(string source)=>QualityOfLifeText.Get(source,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Text="Logiklik 2D Engine";
        ClientSize=new Size(980,590);BackColor=System.Drawing.Color.FromArgb(15,19,27);
        StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10f);
        var identity=new Panel{Dock=DockStyle.None,BackColor=System.Drawing.Color.FromArgb(240,244,249),Padding=new Padding(24)};
        var form=new Panel{Dock=DockStyle.None,Padding=new Padding(40),BackColor=BackColor};
        identity.SetBounds(0,0,470,590);form.SetBounds(470,0,510,590);
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
        using(var stream=typeof(FrmLogin).Assembly.GetManifestResourceStream("Intersect.Editor.studio-logiklik.png"))
            if(stream!=null){using var image=Image.FromStream(stream);picLogo.Image=new Bitmap(image);}
        picLogo.BackgroundImage=null;picLogo.BackColor=identity.BackColor;picLogo.SizeMode=PictureBoxSizeMode.Zoom;picLogo.Dock=DockStyle.Fill;
        identity.Controls.Add(picLogo);picLogo.Show();
        var brand=new Label{Text="LOGIKLIK 2D ENGINE",Dock=DockStyle.Bottom,Height=44,TextAlign=ContentAlignment.MiddleCenter,ForeColor=System.Drawing.Color.FromArgb(32,57,67),Font=new Font("Segoe UI",15,FontStyle.Bold)};
        identity.Controls.Add(brand);
        var heading=new Label{Text=T("Your world starts here"),AutoSize=false,ForeColor=System.Drawing.Color.White,Font=new Font("Segoe UI",23,FontStyle.Bold)};
        heading.SetBounds(36,56,430,70);form.Controls.Add(heading);
        var subtitle=new Label{Text=T("Sign in to build, create and bring your game to life."),ForeColor=System.Drawing.Color.FromArgb(162,179,198)};
        subtitle.SetBounds(40,130,410,58);form.Controls.Add(subtitle);
        lblUsername.ForeColor=System.Drawing.Color.Gainsboro;lblPassword.ForeColor=System.Drawing.Color.Gainsboro;chkRemember.ForeColor=System.Drawing.Color.Gainsboro;
        lblUsername.SetBounds(40,206,400,24);txtUsername.SetBounds(40,236,400,32);
        lblPassword.SetBounds(40,290,400,24);txtPassword.SetBounds(40,320,400,32);
        chkRemember.SetBounds(40,372,400,26);btnLogin.SetBounds(40,420,400,44);
        btnLogin.BackColor=System.Drawing.Color.FromArgb(69,111,35);btnLogin.ForeColor=System.Drawing.Color.White;
        lblStatus.AutoSize=false;lblStatus.SetBounds(40,477,400,44);lblStatus.Font=new Font("Segoe UI",9);
        lblVersion.AutoSize=false;lblVersion.SetBounds(40,545,400,24);lblVersion.Text="Logiklik 2D Engine · "+BuildIdentity.Label;
        lblVersion.Font=new Font("Segoe UI",9);lblVersion.ForeColor=System.Drawing.Color.FromArgb(134,153,173);
        form.Controls.AddRange([lblUsername,txtUsername,lblPassword,txtPassword,chkRemember,btnLogin,lblStatus,lblVersion]);
        lblGettingStarted.Hide();Controls.Add(form);Controls.Add(identity);form.BringToFront();identity.BringToFront();
        AcceptButton=btnLogin;
    }
}
