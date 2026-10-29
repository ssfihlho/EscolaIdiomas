using System;
using System.Data;
using System.Windows.Forms;

namespace EscolaIdiomx
{
    /// <summary>
    /// Tela de LANÇAMENTO de novas avaliações (aberta pelo AvaliacoesCRUD).
    ///
    /// Controles usados: cmbAluno, cmbTurma, txtNota, txtObservacao, dtpData
    /// e o botão butAcessar (que aqui grava o lançamento).
    /// </summary>
    public partial class LancamentoAvaliacoes : Form
    {
        /// <summary>
        /// Indica se ao menos uma avaliação foi lançada.
        /// O AvaliacoesCRUD consulta isso para saber se precisa recarregar a grade.
        /// </summary>
        public bool HouveLancamento { get; private set; }

        public LancamentoAvaliacoes()
        {
            InitializeComponent();
        }

        private void LancamentoAvaliacoes_Load(object sender, EventArgs e)
        {
            // CORREÇÃO: antes os combos nunca eram carregados, então
            // SelectedValue era sempre nulo e nenhum lançamento era gravado.
            AvaliacaoHelper.CarregarComboAlunos(cmbAluno);
            AvaliacaoHelper.CarregarComboTurmas(cmbTurma);

            dtpData.Value = DateTime.Now;
            cmbAluno.Focus();
        }

        /// <summary>Valida os campos e grava a avaliação.</summary>
        private void butAcessar_Click(object sender, EventArgs e)
        {
            if (cmbAluno.SelectedValue == null)
            {
                Avisar("Selecione o aluno.", cmbAluno);
                return;
            }

            DataRowView turma = cmbTurma.SelectedItem as DataRowView;

            if (cmbTurma.SelectedValue == null || turma == null)
            {
                Avisar("Selecione a turma.", cmbTurma);
                return;
            }

            decimal nota;
            if (!AvaliacaoHelper.TentarLerNota(txtNota.Text, out nota))
            {
                Avisar("Informe uma nota válida (de 0 a 10, até 2 casas decimais).", txtNota);
                return;
            }

            int alunoCodigo = Convert.ToInt32(cmbAluno.SelectedValue);
            int turmaCodigo = Convert.ToInt32(cmbTurma.SelectedValue);
            int cursoCodigo = Convert.ToInt32(turma["CursoCodigo"]);

            bool lancado = new DAO().CadastrarAvaliacao(
                alunoCodigo, cursoCodigo, turmaCodigo,
                nota, txtObservacao.Text.Trim(), dtpData.Value);

            if (!lancado)
                return;

            HouveLancamento = true;

            MessageBox.Show("Avaliação lançada com sucesso!", "Sucesso",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Mantém aluno, turma e data para agilizar vários lançamentos seguidos.
            txtNota.Clear();
            txtObservacao.Clear();
            txtNota.Focus();
        }

        private static void Avisar(string mensagem, Control campo)
        {
            MessageBox.Show(mensagem, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            campo.Focus();
        }

        private void btnVoltar_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}