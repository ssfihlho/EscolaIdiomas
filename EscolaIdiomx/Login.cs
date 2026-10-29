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
    public partial class Login : Form
    {
        public Login()
        {
            InitializeComponent();
        }

        private void btnVoltar_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void butAcessar_Click(object sender, EventArgs e)
        {
            string login = txtUsuario.Text.Trim(); // Aceita CPF ou E-mail
            string senha = txtSenha.Text;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
            {
                MessageBox.Show("Informe o CPF/E-mail e a senha.", "Campos Obrigatórios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUsuario.Focus();
                return;
            }

            DAO dao = new DAO();

            // 1. Recebe o objeto do tipo UsuarioAutenticado
            UsuarioAutenticado usuarioLogado = dao.AutenticarUsuario(login, senha);

            // 2. Valida se o retorno é diferente de null (login com sucesso)
            if (usuarioLogado != null)
            {
                // Alimenta a sessão estática global do sistema
                SessaoUsuario.Codigo = usuarioLogado.Codigo;
                SessaoUsuario.Nome = usuarioLogado.Nome;
                SessaoUsuario.Perfil = usuarioLogado.Perfil;

                MessageBox.Show($"Bem-vindo(a), {usuarioLogado.Nome}!", "Login Realizado", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.Hide(); // Esconde a tela de login

                // 3. Redireciona de acordo com o perfil retornado do MySQL
                switch (usuarioLogado.Perfil)
                {
                    case "ADMIN":
                        Menu menuAdmin = new Menu();
                        menuAdmin.ShowDialog();
                        break;

                    case "SECRETARIA":
                        MenuSecretaria menuSec = new MenuSecretaria();
                        menuSec.ShowDialog();
                        break;

                    case "DOCENTE":
                        MenuDocente menuDoc = new MenuDocente();
                        menuDoc.ShowDialog();
                        break;

                    default:
                        MessageBox.Show("Perfil de acesso não reconhecido.", "Acesso Impedido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                }

                this.Close(); // Encerra a aplicação ao fechar a janela do menu
            }
            else
            {
                MessageBox.Show("CPF/E-mail ou senha incorretos, ou usuário inativo.", "Acesso Negado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtSenha.Clear();
                txtSenha.Focus();
            }
        }



        private bool ValidarCPF(string cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
                return false;

            // Rejeitar CPFs com todos os dígitos iguais
            if (cpf.Distinct().Count() == 1)
                return false;

            int soma = 0;

            for (int i = 0; i < 9; i++)
            {
                soma += (cpf[i] - '0') * (10 - i);
            }

            int resto = soma % 11;
            int digito1 = resto < 2 ? 0 : 11 - resto;

            if (digito1 != cpf[9] - '0')
                return false;

            soma = 0;

            for (int i = 0; i < 10; i++)
            {
                soma += (cpf[i] - '0') * (11 - i);
            }

            resto = soma % 11;
            int digito2 = resto < 2 ? 0 : 11 - resto;

            return digito2 == cpf[10] - '0';
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void Login_Load(object sender, EventArgs e)
        {

        }
    }
}
