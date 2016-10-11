using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Polony.UI.WinForm.Logging;
using Color = System.Drawing.Color;

namespace Polony.UI.WinForm
{
    public partial class MainForm : Form
    {
        private readonly PolonyBot _bot;
        private static readonly ILog _log = LogManager.GetLogger(typeof (PolonyBot));

        public MainForm()
        {
            InitializeComponent();

            ConfigureLogging();
            _bot = new PolonyBot(_log);
            _bot.Connect();
        }

        private RichTextBoxAppender _richTextBoxAppender;

        private void ConfigureLogging()
        {
            XmlConfigurator.Configure();

            _richTextBoxAppender = new RichTextBoxAppender(rtfLog)
            {
                Threshold = Level.All,
                Layout = new PatternLayout("%date{dd-MM-yyyy HH:mm:ss.fff} %5level %message %n")
            };
            var infoTextStyle = new LevelTextStyle
            {
                Level = Level.Info,
                TextColor = Color.White,
                PointSize = 10.0f
            };
            _richTextBoxAppender.AddMapping(infoTextStyle);

            var debuTextStyle = new LevelTextStyle
            {
                Level = Level.Debug,
                TextColor = Color.LightBlue,
                PointSize = 10.0f
            };
            _richTextBoxAppender.AddMapping(debuTextStyle);

            var warnTextStyle = new LevelTextStyle
            {
                Level = Level.Warn,
                TextColor = Color.Yellow,
                PointSize = 10.0f
            };
            _richTextBoxAppender.AddMapping(warnTextStyle);

            var errorTextStyle = new LevelTextStyle
            {
                Level = Level.Error,
                TextColor = Color.Red,
                PointSize = 10.0f
            };
            _richTextBoxAppender.AddMapping(errorTextStyle);

            BasicConfigurator.Configure(_richTextBoxAppender);
            _richTextBoxAppender.ActivateOptions();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                _bot.Disconnect();
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }
            finally
            { 
                notifyIcon.Visible = false;
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == this.WindowState)
            {
                notifyIcon.Visible = true;
                this.ShowInTaskbar = false;
            }
            else if (FormWindowState.Normal == this.WindowState)
            {
                notifyIcon.Visible = false;
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            notifyIcon.Visible = false;
        }

        private void rtfLog_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            _bot.Connect();
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            _bot.Disconnect();
        }

        private async void btnScores_Click(object sender, EventArgs e)
        {
            await _bot.ExecuteCommand("SCORES");
        }
    }
}
