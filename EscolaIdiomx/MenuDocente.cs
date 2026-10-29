using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EscolaIdiomx
{
    public partial class MenuDocente : Form
    {
        public MenuDocente()
        {
            InitializeComponent();
        }

        private void btnAvaliacoes_Click(object sender, EventArgs e)
        {
            AvaliacoesCRUD gerenAvaliacoes = new AvaliacoesCRUD();
            gerenAvaliacoes.ShowDialog();
        }

        private void btnHistoricoAluno_Click(object sender, EventArgs e)
        {
            HistoricoDoAluno historicoDoAluno = new HistoricoDoAluno();
            historicoDoAluno.ShowDialog();
        }

        private void button1_Click(object sender, EventArgs e)//LOGOFF
        {
            SessaoUsuario.EncerrarSessao();
            this.Close(); // Fecha o menu atual, fazendo o programa retornar à tela de login ou encerrar

        }
    }
}
