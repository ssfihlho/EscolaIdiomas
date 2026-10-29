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
    public partial class UsuariosCRUD : Form
    {
        private int? usuarioSelecionado = null;
        private bool modoEdicao = false;
        public UsuariosCRUD()
        {
            InitializeComponent();
            dataGridView1.CellClick += dataGridView1_CellClick;
        }

        private void butAcessar_Click(object sender, EventArgs e)
        {
            CadastrarUsuarios cadastrarUx = new CadastrarUsuarios();
            cadastrarUx.ShowDialog();
        }

        private void btnVoltar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void UsuariosCRUD_Load(object sender, EventArgs e)
        {
            CarregarComboPerfil();
            CarregarUsuarios();
            LimparCampos();
        
        }

        private void CarregarComboPerfil()
        {
            cmbTipoUsuario.Items.Clear();
            cmbTipoUsuario.Items.Add("ADMIN");
            cmbTipoUsuario.Items.Add("SECRETARIA");
            cmbTipoUsuario.Items.Add("DOCENTE");
            cmbTipoUsuario.SelectedIndex = -1;
        }

        private void CarregarUsuarios(string filtro = "")
        {
            DAO dao = new DAO();
            dataGridView1.DataSource = dao.ListarUsuarios(filtro);

            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (dataGridView1.Columns.Contains("Codigo"))
                dataGridView1.Columns["Codigo"].Visible = false;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string perfilSelecionado = cmbTipoUsuario.SelectedItem?.ToString().ToUpper();

            // Se o usuário selecionado for DOCENTE, habilita/exibe os campos detalhados
            bool ehDocente = (perfilSelecionado == "DOCENTE");

            // Exemplo de controle de visibilidade/habilitação dos campos extras
            txtTelefone.Enabled = ehDocente;
            dtpDtNasc.Enabled = ehDocente; // ou mtbDataNascimento
            txtCidade.Enabled = ehDocente;
            txtEstado.Enabled = ehDocente;
            txtBairro.Enabled = ehDocente;
            txtLogradouro.Enabled = ehDocente;
            txtNum.Enabled = ehDocente;
            txtComplemento.Enabled = ehDocente;
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridView1.Rows.Count)
                return;

            DataGridViewRow linha = dataGridView1.Rows[e.RowIndex];

            if (linha.IsNewRow || linha.Cells["Codigo"].Value == null || linha.Cells["Codigo"].Value == DBNull.Value)
                return;

            usuarioSelecionado = Convert.ToInt32(linha.Cells["Codigo"].Value);

            txtNome.Text = Convert.ToString(linha.Cells["Nome"].Value);
            txtCpf.Text = Convert.ToString(linha.Cells["CPF"].Value);
            txtEmail.Text = Convert.ToString(linha.Cells["Email"].Value);

            if (linha.Cells["Perfil"].Value != DBNull.Value)
                cmbTipoUsuario.SelectedItem = Convert.ToString(linha.Cells["Perfil"].Value);

            modoEdicao = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!usuarioSelecionado.HasValue)
            {
                MessageBox.Show("Selecione um usuário na tabela para editar.", "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            modoEdicao = true;
            txtNome.Focus();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNome.Text) || string.IsNullOrWhiteSpace(txtCpf.Text) ||
                string.IsNullOrWhiteSpace(txtEmail.Text) || cmbTipoUsuario.SelectedItem == null)
            {
                MessageBox.Show("Preencha Nome, CPF, E-mail e Tipo de Usuário.", "Campos Obrigatórios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string nome = txtNome.Text.Trim();
            string cpf = txtCpf.Text.Trim();
            string email = txtEmail.Text.Trim();
            string perfil = cmbTipoUsuario.SelectedItem.ToString();

            DAO dao = new DAO();
            bool sucesso;

            if (modoEdicao && usuarioSelecionado.HasValue)
            {
                // Atualização
                sucesso = dao.AtualizarUsuario(usuarioSelecionado.Value, nome, cpf, email, "", perfil);
            }
            else
            {
                // Novo Cadastro (Senha padrão inicial: 123456)
                sucesso = dao.CadastrarUsuario(nome, cpf, email, "123456", perfil);
            }

            if (sucesso)
            {
                MessageBox.Show("Usuário salvo com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                CarregarUsuarios();
                LimparCampos();
            }
        }

        private void LimparCampos()
        {
            usuarioSelecionado = null;
            modoEdicao = false;

            txtNome.Clear();
            txtCpf.Clear();
            txtEmail.Clear();
            cmbTipoUsuario.SelectedIndex = -1;

            // Limpa os demais campos da tela
            if (txtTelefone != null) txtTelefone.Clear();
            if (txtCidade != null) txtCidade.Clear();
            if (txtEstado != null) txtEstado.Clear();
            if (txtBairro != null) txtBairro.Clear();
            if (txtLogradouro != null) txtLogradouro.Clear();
            if (txtNum != null) txtNum.Clear();
            if (txtComplemento != null) txtComplemento.Clear();
        }

        private void btnExcluir_Click(object sender, EventArgs e)
        {
            if (!usuarioSelecionado.HasValue)
            {
                MessageBox.Show("Selecione um usuário na tabela antes de excluir.", "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Deseja realmente excluir este usuário?", "Confirmar Exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            DAO dao = new DAO();
            if (dao.ExcluirUsuario(usuarioSelecionado.Value))
            {
                MessageBox.Show("Usuário excluído com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                CarregarUsuarios();
                LimparCampos();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            CarregarUsuarios(txtBuscar.Text.Trim());
        }

        private void button5_Click(object sender, EventArgs e)
        {
            txtBuscar.Clear();
            CarregarUsuarios();
        }
    }
}
