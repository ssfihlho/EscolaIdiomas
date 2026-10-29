using System;
using System.Data;
using System.Globalization;
using System.Windows.Forms;

namespace EscolaIdiomx
{
    public partial class HistoricoDoAluno : Form
    {
        public HistoricoDoAluno()
        {
            InitializeComponent();
        }

        private void HistoricoDoAluno_Load(object sender, EventArgs e)
        {
            txtBuscar.Focus();
            // Traz todo o histórico cadastrado assim que a tela abre
            BuscarHistorico();
        }

        private void button1_Click(object sender, EventArgs e) // Botão de Pesquisar
        {
            BuscarHistorico();
        }

        private void txtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            // Dispara a busca ao pressionar ENTER no campo de texto
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                BuscarHistorico();
            }
        }

        private void BuscarHistorico()
        {
            string filtro = txtBuscar.Text.Trim();

            DAO dao = new DAO();
            DataTable dt = dao.ObterHistoricoAlunoPorFiltro(filtro);

            dgvHistorico.DataSource = dt;
            dgvHistorico.ReadOnly = true;
            dgvHistorico.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvHistorico.MultiSelect = false;
            dgvHistorico.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Oculta a coluna de ID primário mantendo os dados visíveis ao usuário
            if (dgvHistorico.Columns.Contains("Codigo"))
                dgvHistorico.Columns["Codigo"].Visible = false;

            // Formatação dos cabeçalhos
            if (dgvHistorico.Columns.Contains("Data"))
                dgvHistorico.Columns["Data"].DefaultCellStyle.Format = "dd/MM/yyyy";

        
        }

       

        private void btnVoltar_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}