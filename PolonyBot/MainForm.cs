using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Discord;
using Discord.Commands;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Polony.Logging;

using Color = System.Drawing.Color;

namespace Polony
{
    public partial class MainForm : Form
    {
        private readonly PolonyBot _bot;
        private static readonly ILog _log = LogManager.GetLogger(typeof (PolonyBot));

        public MainForm()
        {
            InitializeComponent();

            ConfigureLogging();
            _bot = new PolonyBot(_log, ConfigurationManager.ConnectionStrings["DbConnectionString"].ConnectionString);
            _bot.Initialize(Properties.Settings.Default.BotToken);
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
            _bot.Disconnect();
            notifyIcon.Visible = false;
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
    }
}
