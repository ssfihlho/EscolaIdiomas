using System;
using System.Windows.Forms;

namespace EscolaIdiomx
{
    /// <summary>
    /// Tela de CONSULTA, EDIÇÃO e EXCLUSÃO de avaliações.
    /// Novas avaliações são lançadas na tela LancamentoAvaliacoes (botão butAcessar).
    ///
    /// Controles usados: dgvAvaliacoes, cmbAluno, cmbTurma, txtNota, txtObservacao,
    /// dtpData, txtBuscar, btnBuscar, btnListarTodos, btnEditar, btnSalvar,
    /// btnExcluir, btnVoltar, butAcessar.
    /// </summary>
    public partial class AvaliacoesCRUD : Form
    {
        // Código da avaliação escolhida na grade (null = nenhuma).
        private int? avaliacaoSelecionada = null;

        // true enquanto o usuário edita a avaliação selecionada.
        private bool modoEdicao = false;

        public AvaliacoesCRUD()
        {
            InitializeComponent();

            // "-=" antes de "+=" evita que o evento seja assinado duas vezes
            // caso o Designer também o ligue.
            dgvAvaliacoes.CellClick -= dgvAvaliacoes_CellClick;
            dgvAvaliacoes.CellClick += dgvAvaliacoes_CellClick;
        }

        // ------------------------------------------------------------------
        // CARGA DA TELA
        // ------------------------------------------------------------------

        private void AvaliacoesCRUD_Load(object sender, EventArgs e)
        {
            CarregarCombos();
            CarregarAvaliacoes();
            LimparCampos();
        }

        /// <summary>Alunos e turmas só servem para exibir a avaliação selecionada.</summary>
        private void CarregarCombos()
        {
            AvaliacaoHelper.CarregarComboAlunos(cmbAluno);
            AvaliacaoHelper.CarregarComboTurmas(cmbTurma);
        }

        /// <summary>Carrega a grade (com filtro opcional por aluno ou curso).</summary>
        private void CarregarAvaliacoes(string filtro = "")
        {
            dgvAvaliacoes.DataSource = new DAO().ListarAvaliacoes(filtro);

            dgvAvaliacoes.ReadOnly = true;
            dgvAvaliacoes.AllowUserToAddRows = false;
            dgvAvaliacoes.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvAvaliacoes.MultiSelect = false;
            dgvAvaliacoes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Colunas técnicas ficam ocultas, mas continuam disponíveis no código.
            foreach (string coluna in new[] { "Codigo", "AlunoCodigo", "CursoCodigo", "TurmaCodigo" })
            {
                if (dgvAvaliacoes.Columns.Contains(coluna))
                    dgvAvaliacoes.Columns[coluna].Visible = false;
            }

            if (dgvAvaliacoes.Columns.Contains("Observacao"))
                dgvAvaliacoes.Columns["Observacao"].HeaderText = "Observação";

            if (dgvAvaliacoes.Columns.Contains("Data"))
                dgvAvaliacoes.Columns["Data"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";

            // Sem linha pré-selecionada: o usuário escolhe clicando.
            dgvAvaliacoes.ClearSelection();
        }

        // ------------------------------------------------------------------
        // SELEÇÃO NA GRADE
        // ------------------------------------------------------------------

        private void dgvAvaliacoes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvAvaliacoes.Rows.Count)
                return;

            DataGridViewRow linha = dgvAvaliacoes.Rows[e.RowIndex];
            object codigo = linha.Cells["Codigo"].Value;

            if (linha.IsNewRow || codigo == null || codigo == DBNull.Value)
                return;

            avaliacaoSelecionada = Convert.ToInt32(codigo);

            SelecionarNoCombo(cmbAluno, linha.Cells["AlunoCodigo"].Value);
            SelecionarNoCombo(cmbTurma, linha.Cells["TurmaCodigo"].Value);

            object nota = linha.Cells["Nota"].Value;
            txtNota.Text = (nota == null || nota == DBNull.Value)
                ? string.Empty
                : Convert.ToDecimal(nota).ToString("0.00");

            txtObservacao.Text = Convert.ToString(linha.Cells["Observacao"].Value);

            if (linha.Cells["Data"].Value is DateTime)
                dtpData.Value = (DateTime)linha.Cells["Data"].Value;

            // Ao selecionar, a tela abre somente para leitura.
            modoEdicao = false;
            DefinirEdicao(false);
        }

        private static void SelecionarNoCombo(ComboBox combo, object valor)
        {
            if (valor == null || valor == DBNull.Value)
                combo.SelectedIndex = -1;
            else
                combo.SelectedValue = Convert.ToInt32(valor);
        }

        // ------------------------------------------------------------------
        // EDITAR / SALVAR / EXCLUIR
        // ------------------------------------------------------------------

        private void btnEditar_Click_1(object sender, EventArgs e)
        {
            if (!avaliacaoSelecionada.HasValue)
            {
                MessageBox.Show("Selecione uma avaliação na tabela.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            modoEdicao = true;
            DefinirEdicao(true);
            txtNota.Focus();
        }

        /// <summary>Salva apenas ALTERAÇÕES (nota, observação, data). Novos lançamentos têm tela própria.</summary>
        private void btnSalvar_Click_1(object sender, EventArgs e)
        {
            if (!modoEdicao || !avaliacaoSelecionada.HasValue)
            {
                MessageBox.Show(
                    "Para alterar, selecione uma avaliação e clique em Editar.\n" +
                    "Para lançar uma nova avaliação, use o botão de lançamento.",
                    "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            decimal nota;
            if (!AvaliacaoHelper.TentarLerNota(txtNota.Text, out nota))
            {
                MessageBox.Show("Informe uma nota válida (de 0 a 10, até 2 casas decimais).",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNota.Focus();
                return;
            }

            bool sucesso = new DAO().AtualizarAvaliacao(
                avaliacaoSelecionada.Value, nota, txtObservacao.Text.Trim(), dtpData.Value);

            if (sucesso)
            {
                MessageBox.Show("Avaliação atualizada com sucesso!", "Sucesso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CarregarAvaliacoes(txtBuscar.Text.Trim());
                LimparCampos();
            }
        }

        private void btnExcluir_Click(object sender, EventArgs e)
        {
            if (!avaliacaoSelecionada.HasValue)
            {
                MessageBox.Show("Selecione uma avaliação na tabela antes de excluir.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirmacao = MessageBox.Show(
                "Tem certeza de que deseja excluir esta avaliação?",
                "Confirmar Exclusão",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmacao != DialogResult.Yes)
                return;

            if (new DAO().ExcluirAvaliacao(avaliacaoSelecionada.Value))
            {
                MessageBox.Show("Avaliação excluída com sucesso!", "Sucesso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CarregarAvaliacoes(txtBuscar.Text.Trim());
                LimparCampos();
            }
        }

        // ------------------------------------------------------------------
        // BUSCA
        // ------------------------------------------------------------------

        private void btnBuscar_Click(object sender, EventArgs e)
        {
            CarregarAvaliacoes(txtBuscar.Text.Trim());
            LimparCampos();
        }

        private void btnListarTodos_Click(object sender, EventArgs e)
        {
            txtBuscar.Clear();
            CarregarAvaliacoes();
            LimparCampos();
        }

        // ------------------------------------------------------------------
        // TELA DE LANÇAMENTO
        // ------------------------------------------------------------------

        /// <summary>
        /// Abre a tela de lançamento. Ao voltar, se algo foi lançado,
        /// recarrega combos e grade para as duas telas ficarem sincronizadas.
        /// </summary>
        private void butAcessar_Click(object sender, EventArgs e)
        {
            using (LancamentoAvaliacoes lancamento = new LancamentoAvaliacoes())
            {
                lancamento.ShowDialog(this);

                if (lancamento.HouveLancamento)
                {
                    CarregarCombos();
                    CarregarAvaliacoes(txtBuscar.Text.Trim());
                    LimparCampos();
                }
            }
        }

        // ------------------------------------------------------------------
        // ESTADO DA TELA
        // ------------------------------------------------------------------

        /// <summary>
        /// Habilita/desabilita os campos editáveis e os botões conforme o estado.
        /// Aluno e turma nunca são editáveis aqui: a avaliação pertence a eles.
        /// </summary>
        private void DefinirEdicao(bool editando)
        {
            cmbAluno.Enabled = false;
            cmbTurma.Enabled = false;

            txtNota.ReadOnly = !editando;
            txtObservacao.ReadOnly = !editando;
            dtpData.Enabled = editando;

            btnSalvar.Enabled = editando;
            btnEditar.Enabled = !editando && avaliacaoSelecionada.HasValue;
            btnExcluir.Enabled = avaliacaoSelecionada.HasValue;
        }

        private void LimparCampos()
        {
            avaliacaoSelecionada = null;
            modoEdicao = false;

            cmbAluno.SelectedIndex = -1;
            cmbTurma.SelectedIndex = -1;
            txtNota.Clear();
            txtObservacao.Clear();
            dtpData.Value = DateTime.Now;

            DefinirEdicao(false);
            dgvAvaliacoes.ClearSelection();
        }

        // ------------------------------------------------------------------
        // FECHAR
        // ------------------------------------------------------------------

        private void btnVoltar_Click(object sender, EventArgs e)
        {
            Close();
        }

        // Duplicado do anterior (o Designer pode estar ligado a qualquer um dos dois).
        // Se apenas um deles estiver em uso, o outro pode ser apagado.
        private void btnVoltar_Click_1(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}