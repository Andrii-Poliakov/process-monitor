namespace ProcessMonitorDebugUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void buttonGetProcessList_Click(object sender, EventArgs e)
        {
            var helper = new ProcessManager.ProcessHelper();
            var list = helper.GetProcessList();

            var text = System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });


            textBox1.Text = text;
        }
    }
}
