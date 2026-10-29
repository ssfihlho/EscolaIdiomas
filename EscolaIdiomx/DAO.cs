using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace EscolaIdiomx
{
    /// <summary>
    /// Camada de acesso a dados (DAO) da escola de idiomas.
    /// Contém: conexão, autenticação e CRUD de usuários, alunos, professores,
    /// idiomas, cursos, turmas, matrículas e avaliações.
    /// </summary>
    internal class DAO
    {
        // ATENÇÃO: root sem senha serve apenas para desenvolvimento.
        // Em produção use um usuário próprio e guarde isto no App.config.
        private const string StringConexao =
            "server=localhost;Database=escolaIdiomas;Uid=root;password=";

        // Códigos de erro do MySQL tratados de forma amigável.
        private const int ErroChaveDuplicada = 1062;   // violação de UNIQUE
        private const int ErroChaveEstrangeira = 1451; // registro em uso por outra tabela

        // Perfis aceitos pelo sistema.
        private static readonly string[] PerfisValidos = { "ADMIN", "SECRETARIA", "DOCENTE" };

        // Conexão única da instância: aberta no início de cada método e fechada no "finally".
        public MySqlConnection conexao;

        public DAO()
        {
            conexao = new MySqlConnection(StringConexao);
        }

        // ==================================================================
        // CONEXÃO
        // ==================================================================

        /// <summary>Abre a conexão caso ainda não esteja aberta.</summary>
        /// <returns>true se a conexão está aberta; false se houve falha.</returns>
        public bool Conectar()
        {
            try
            {
                // Uma conexão "quebrada" precisa ser fechada antes de reabrir.
                if (conexao.State == ConnectionState.Broken)
                    conexao.Close();

                if (conexao.State != ConnectionState.Open)
                    conexao.Open();

                return true;
            }
            catch (MySqlException erro)
            {
                MostrarErro("Erro ao conectar ao banco:\n" + erro.Message);
                return false;
            }
        }

        /// <summary>Fecha a conexão caso esteja aberta.</summary>
        public void Desconectar()
        {
            if (conexao != null && conexao.State != ConnectionState.Closed)
                conexao.Close();
        }

        // ==================================================================
        // USUÁRIOS E AUTENTICAÇÃO
        // ==================================================================

        /// <summary>
        /// Autentica um usuário ativo por E-MAIL ou CPF e senha.
        /// O CPF pode ser digitado com ou sem pontuação, e funciona
        /// independentemente de como foi gravado no banco.
        /// </summary>
        /// <returns>Dados do usuário, ou null se as credenciais forem inválidas.</returns>
        public UsuarioAutenticado AutenticarUsuario(string login, string senha)
        {
            // Evita consulta e erro do BCrypt com valores vazios.
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(senha))
                return null;

            // Se tiver "@" é e-mail; caso contrário é CPF (só dígitos).
            login = login.Trim();
            bool ehEmail = login.Contains("@");
            string valor = ehEmail ? login : SomenteDigitos(login);

            if (valor.Length == 0)
                return null;

            if (!Conectar())
                return null;

            try
            {
                string condicao = ehEmail ? "email = @login" : ExprCpf("cpf") + " = @login";

                string sql = @"
                    SELECT codigo, nome, email, senhaHash, perfil
                    FROM usuario
                    WHERE " + condicao + @"
                      AND ativo = 1
                    LIMIT 1;";

                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                {
                    cmd.Parameters.Add(P("@login", valor));

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Usuário não encontrado ou inativo.
                        if (!reader.Read())
                            return null;

                        string hash = Convert.ToString(reader["senhaHash"]);

                        // Senha vazia no banco.
                        if (string.IsNullOrEmpty(hash))
                        {
                            MostrarAviso("O hash da senha armazenado no banco é inválido.", "Erro de Segurança");
                            return null;
                        }

                        if (!BCrypt.Net.BCrypt.Verify(senha, hash))
                            return null;

                        return new UsuarioAutenticado
                        {
                            Codigo = Convert.ToInt32(reader["codigo"]),
                            Nome = Convert.ToString(reader["nome"]),
                            Email = Convert.ToString(reader["email"]),
                            Perfil = Convert.ToString(reader["perfil"]).Trim().ToUpperInvariant()
                        };
                    }
                }
            }
            catch (MySqlException erro)
            {
                MostrarErro("Erro ao realizar autenticação:\n" + erro.Message, "Erro no Banco");
                return null;
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Hash gravado fora do formato BCrypt (ex.: senha em texto puro).
                MostrarAviso("O hash da senha armazenado no banco é inválido.", "Erro de Segurança");
                return null;
            }
            catch (Exception ex)
            {
                MostrarErro("Erro inesperado na autenticação:\n" + ex.Message);
                return null;
            }
            finally
            {
                Desconectar();
            }
        }

        /// <summary>
        /// Cadastra um usuário (ADMIN, SECRETARIA ou DOCENTE) com senha em hash BCrypt.
        /// </summary>
        public bool CadastrarUsuario(string nome, string cpf, string email, string senha, string perfil)
        {
            return CadastrarUsuarioEObterId(nome, cpf, email, senha, perfil) > 0;
        }

        /// <summary>
        /// Cadastra o usuário e retorna o código gerado (AUTO_INCREMENT).
        /// Retorna -1 em caso de falha ou duplicidade.
        /// </summary>
        public int CadastrarUsuarioEObterId(string nome, string cpf, string email, string senha, string perfil)
        {
            perfil = NormalizarPerfil(perfil);

            if (TemCampoVazio(nome, cpf, email, senha) || perfil == null)
                return -1;

            if (!Conectar())
                return -1;

            try
            {
                const string sql = @"
                    INSERT INTO usuario
                        (nome, cpf, email, senhaHash, perfil, ativo, senhaDefinida)
                    VALUES
                        (@nome, @cpf, @email, @senhaHash, @perfil, TRUE, TRUE);";

                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                {
                    cmd.Parameters.Add(P("@nome", nome.Trim()));
                    cmd.Parameters.Add(P("@cpf", cpf.Trim()));
                    cmd.Parameters.Add(P("@email", email.Trim()));
                    // Nunca se grava a senha pura: apenas o hash.
                    cmd.Parameters.Add(P("@senhaHash", BCrypt.Net.BCrypt.HashPassword(senha)));
                    cmd.Parameters.Add(P("@perfil", perfil));

                    cmd.ExecuteNonQuery();
                    return (int)cmd.LastInsertedId;
                }
            }
            catch (MySqlException erro) when (erro.Number == ErroChaveDuplicada)
            {
                MostrarAviso("Já existe um usuário com esse CPF ou e-mail.", "Cadastro Duplicado");
                return -1;
            }
            catch (Exception erro)
            {
                MostrarErro("Erro ao cadastrar usuário:\n" + erro.Message);
                return -1;
            }
            finally
            {
                Desconectar();
            }
        }

        /// <summary>Consulta usuários filtrando por nome ou CPF.</summary>
        public DataTable ListarUsuarios(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();
            string digitos = SomenteDigitos(filtro);

            string sql = @"
                SELECT
                    codigo AS Codigo,
                    nome   AS Nome,
                    cpf    AS CPF,
                    email  AS Email,
                    perfil AS Perfil,
                    ativo  AS Ativo
                FROM usuario
                WHERE nome LIKE @filtro" + CondicaoCpf("cpf", digitos) + @"
                ORDER BY codigo DESC;";

            return Consultar(sql, "Erro ao listar usuários", ParametrosBusca(filtro, digitos));
        }

        /// <summary>
        /// Atualiza um usuário. A senha só é alterada se "novaSenha" for informada.
        /// </summary>
        public bool AtualizarUsuario(int codigo, string nome, string cpf, string email, string novaSenha, string perfil)
        {
            perfil = NormalizarPerfil(perfil);

            if (codigo <= 0 || TemCampoVazio(nome, cpf, email) || perfil == null)
            {
                MostrarAviso("Preencha nome, CPF, e-mail e perfil corretamente.");
                return false;
            }

            bool mudarSenha = !string.IsNullOrWhiteSpace(novaSenha);

            string sql = @"
                UPDATE usuario
                SET nome = @nome, cpf = @cpf, email = @email, perfil = @perfil"
                + (mudarSenha ? ", senhaHash = @senhaHash" : string.Empty) + @"
                WHERE codigo = @codigo;";

            List<MySqlParameter> parametros = new List<MySqlParameter>
            {
                P("@nome", nome.Trim()),
                P("@cpf", cpf.Trim()),
                P("@email", email.Trim()),
                P("@perfil", perfil),
                P("@codigo", codigo)
            };

            if (mudarSenha)
                parametros.Add(P("@senhaHash", BCrypt.Net.BCrypt.HashPassword(novaSenha)));

            return ExecutarUnico(
                sql,
                "Erro ao atualizar usuário",
                "Já existe outro usuário com esse CPF ou e-mail.",
                null,
                parametros.ToArray());
        }

        /// <summary>Exclui um usuário (não permite excluir o próprio usuário logado).</summary>
        public bool ExcluirUsuario(int codigo)
        {
            if (codigo <= 0)
                return false;

            if (codigo == SessaoUsuario.Codigo)
            {
                MostrarAviso("Você não pode excluir o seu próprio usuário logado.");
                return false;
            }

            return ExecutarUnico(
                "DELETE FROM usuario WHERE codigo = @codigo;",
                "Erro ao excluir usuário",
                null,
                "Não é possível excluir: o usuário possui registros vinculados.",
                P("@codigo", codigo));
        }

        // ==================================================================
        // ALUNOS
        // ==================================================================

        /// <summary>
        /// Cadastra endereço e aluno em uma única transação (ou grava os dois, ou nenhum).
        /// </summary>
        public bool CadastrarAlunoComEndereco(
            string nome,
            long cpf,
            string email,
            long telefone,
            DateTime dataNascimento,
            string cidade,
            string estado,
            string bairro,
            string logradouro,
            int numero,
            string complemento)
        {
            // Validação de entrada (complemento é opcional).
            if (TemCampoVazio(nome, email, cidade, estado, bairro, logradouro)
                || cpf <= 0
                || telefone <= 0
                || numero < 0
                || !DataNascimentoValida(dataNascimento))
            {
                MostrarAviso("Preencha todos os campos obrigatórios corretamente.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                // 1. Endereço.
                long enderecoCodigo = InserirEndereco(
                    transacao, cidade, estado, bairro, logradouro, numero.ToString(), complemento);

                // 2. Aluno vinculado ao endereço recém-criado.
                const string sqlAluno = @"
                    INSERT INTO aluno
                        (nome, cpf, email, telefone, dataNascimento, enderecoCodigo)
                    VALUES
                        (@nome, @cpf, @email, @telefone, @dataNascimento, @enderecoCodigo);";

                using (MySqlCommand cmd = new MySqlCommand(sqlAluno, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@nome", nome.Trim()));
                    cmd.Parameters.Add(P("@cpf", cpf));
                    cmd.Parameters.Add(P("@email", email.Trim()));
                    cmd.Parameters.Add(P("@telefone", telefone));
                    cmd.Parameters.Add(P("@dataNascimento", dataNascimento));
                    cmd.Parameters.Add(P("@enderecoCodigo", enderecoCodigo));
                    cmd.ExecuteNonQuery();
                }

                // 3. Confirma as duas operações juntas.
                transacao.Commit();
                return true;
            }
            catch (MySqlException erro) when (erro.Number == ErroChaveDuplicada)
            {
                Reverter(transacao);
                MostrarAviso("Já existe um aluno com esse CPF ou e-mail.", "Cadastro Duplicado");
                return false;
            }
            catch (Exception erro)
            {
                Reverter(transacao);
                MostrarErro("Não foi possível cadastrar aluno e endereço:\n" + erro.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>Lista alunos (com endereço) filtrando por nome ou CPF.</summary>
        public DataTable ListarAlunos(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();
            string digitos = SomenteDigitos(filtro);

            // A condição de CPF só entra quando há dígitos; sem isso,
            // "cpf LIKE '%%'" casaria com todos os registros e anularia a busca por nome.
            string sql = @"
                SELECT
                    a.codigo         AS Codigo,
                    a.nome           AS Nome,
                    a.cpf            AS CPF,
                    a.email          AS Email,
                    a.telefone       AS Telefone,
                    a.dataNascimento AS DataNascimento,
                    a.enderecoCodigo AS EnderecoCodigo,
                    e.cidade         AS Cidade,
                    e.estado         AS Estado,
                    e.bairro         AS Bairro,
                    e.logradouro     AS Logradouro,
                    e.numero         AS Numero,
                    e.complemento    AS Complemento
                FROM aluno a
                LEFT JOIN endereco e ON e.codigo = a.enderecoCodigo
                WHERE a.nome LIKE @filtro" + CondicaoCpf("a.cpf", digitos) + @"
                ORDER BY a.nome;";

            return Consultar(sql, "Erro ao listar alunos", ParametrosBusca(filtro, digitos));
        }

        /// <summary>
        /// Atualiza aluno e endereço em uma transação.
        /// Se o aluno ainda não tiver endereço, um novo é criado.
        /// </summary>
        public bool AtualizarAluno(
            int codigo,
            int? enderecoCodigo,
            string nome,
            string cpf,
            string email,
            string telefone,
            DateTime dataNascimento,
            string cidade,
            string estado,
            string bairro,
            string logradouro,
            string numero,
            string complemento)
        {
            if (codigo <= 0
                || TemCampoVazio(nome, cpf, email, telefone,
                                 cidade, estado, bairro, logradouro, numero)
                || !DataNascimentoValida(dataNascimento))
            {
                MostrarAviso("Preencha todos os campos obrigatórios corretamente.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                long idEndereco = SalvarEndereco(
                    transacao, enderecoCodigo, cidade, estado, bairro, logradouro, numero, complemento);

                const string sqlAluno = @"
                    UPDATE aluno
                    SET nome = @nome,
                        cpf = @cpf,
                        email = @email,
                        telefone = @telefone,
                        dataNascimento = @dataNascimento,
                        enderecoCodigo = @enderecoCodigo
                    WHERE codigo = @codigo;";

                using (MySqlCommand cmd = new MySqlCommand(sqlAluno, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@nome", nome.Trim()));
                    cmd.Parameters.Add(P("@cpf", cpf.Trim()));
                    cmd.Parameters.Add(P("@email", email.Trim()));
                    cmd.Parameters.Add(P("@telefone", telefone.Trim()));
                    cmd.Parameters.Add(P("@dataNascimento", dataNascimento));
                    cmd.Parameters.Add(P("@enderecoCodigo", idEndereco));
                    cmd.Parameters.Add(P("@codigo", codigo));

                    // Nenhuma linha afetada = aluno inexistente; cancela tudo.
                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("O aluno não foi encontrado.");
                }

                transacao.Commit();
                return true;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveDuplicada)
            {
                Reverter(transacao);
                MostrarAviso("Já existe outro aluno com esse CPF ou e-mail.", "Cadastro Duplicado");
                return false;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao atualizar aluno:\n" + ex.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>Exclui um aluno pelo código.</summary>
        public bool ExcluirAluno(int codigo)
        {
            if (codigo <= 0)
                return false;

            return ExecutarUnico(
                "DELETE FROM aluno WHERE codigo = @codigo;",
                "Erro ao excluir aluno",
                null,
                "Não é possível excluir: o aluno possui registros vinculados (matrículas ou avaliações).",
                P("@codigo", codigo));
        }

        // ==================================================================
        // PROFESSORES (usuario + professor + endereco)
        // ==================================================================

        /// <summary>
        /// Cadastra usuário (perfil DOCENTE), endereço e professor em uma única transação.
        /// Se qualquer etapa falhar, nada é gravado.
        /// </summary>
        public bool CadastrarProfessorComEndereco(
            string nome,
            string cpf,
            string email,
            string senha,
            string titulacao,
            string especialidade,
            string cidade,
            string estado,
            string bairro,
            string logradouro,
            string numero,
            string complemento)
        {
            if (TemCampoVazio(nome, cpf, email, senha, cidade, estado, bairro, logradouro, numero))
            {
                MostrarAviso("Preencha todos os campos obrigatórios corretamente.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                // 1. Usuário (dados de login e acesso).
                const string sqlUsuario = @"
                    INSERT INTO usuario
                        (nome, cpf, email, senhaHash, perfil, ativo, senhaDefinida)
                    VALUES
                        (@nome, @cpf, @email, @senhaHash, 'DOCENTE', TRUE, TRUE);";

                long usuarioCodigo;

                using (MySqlCommand cmd = new MySqlCommand(sqlUsuario, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@nome", nome.Trim()));
                    cmd.Parameters.Add(P("@cpf", cpf.Trim()));
                    cmd.Parameters.Add(P("@email", email.Trim()));
                    cmd.Parameters.Add(P("@senhaHash", BCrypt.Net.BCrypt.HashPassword(senha)));
                    cmd.ExecuteNonQuery();
                    usuarioCodigo = cmd.LastInsertedId;
                }

                // 2. Endereço.
                long enderecoCodigo = InserirEndereco(
                    transacao, cidade, estado, bairro, logradouro, numero, complemento);

                // 3. Professor, ligado ao usuário e ao endereço.
                InserirProfessor(transacao, usuarioCodigo, titulacao, especialidade, enderecoCodigo);

                transacao.Commit();
                return true;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveDuplicada)
            {
                Reverter(transacao);
                MostrarAviso("Já existe um usuário cadastrado com esse CPF ou e-mail.", "Cadastro Duplicado");
                return false;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Não foi possível salvar o cadastro do professor:\n" + ex.Message, "Erro no Cadastro");
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>
        /// Cadastra o endereço e o perfil de professor para um usuário já criado
        /// (fluxo em duas telas: 1ª cria o usuário, 2ª completa o professor).
        /// </summary>
        public bool CadastrarDadosProfessorEEndereco(
            int usuarioCodigo,
            string titulacao,
            string especialidade,
            string cidade,
            string estado,
            string bairro,
            string logradouro,
            string numero,
            string complemento)
        {
            if (usuarioCodigo <= 0 || TemCampoVazio(cidade, estado, bairro, logradouro, numero))
            {
                MostrarAviso("Preencha todos os campos obrigatórios do endereço.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                long enderecoCodigo = InserirEndereco(
                    transacao, cidade, estado, bairro, logradouro, numero, complemento);

                InserirProfessor(transacao, usuarioCodigo, titulacao, especialidade, enderecoCodigo);

                transacao.Commit();
                return true;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveDuplicada)
            {
                Reverter(transacao);
                MostrarAviso("Este usuário já possui cadastro de professor.", "Cadastro Duplicado");
                return false;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao salvar dados do docente e endereço:\n" + ex.Message, "Erro de Transação");
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>Lista professores ativos unindo usuario, professor e endereco.</summary>
        public DataTable ListarProfessores(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();
            string digitos = SomenteDigitos(filtro);

            string sql = @"
                SELECT
                    u.codigo         AS Codigo,
                    u.nome           AS Nome,
                    u.cpf            AS CPF,
                    u.email          AS Email,
                    p.titulacao      AS Titulacao,
                    p.especialidade  AS Especialidade,
                    p.dataAdmissao   AS DataAdmissao,
                    p.enderecoCodigo AS EnderecoCodigo,
                    e.cidade         AS Cidade,
                    e.estado         AS Estado,
                    e.bairro         AS Bairro,
                    e.logradouro     AS Logradouro,
                    e.numero         AS Numero,
                    e.complemento    AS Complemento
                FROM usuario u
                INNER JOIN professor p ON u.codigo = p.usuarioCodigo
                LEFT JOIN endereco e ON p.enderecoCodigo = e.codigo
                WHERE u.ativo = 1
                  AND (u.nome LIKE @filtro" + CondicaoCpf("u.cpf", digitos) + @")
                ORDER BY u.nome;";

            return Consultar(sql, "Erro ao consultar professores", ParametrosBusca(filtro, digitos));
        }

        /// <summary>Atualiza usuario, endereco e professor em uma transação.</summary>
        public bool AtualizarProfessorComEndereco(
            int usuarioCodigo,
            int? enderecoCodigo,
            string nome,
            string cpf,
            string email,
            string titulacao,
            string especialidade,
            string cidade,
            string estado,
            string bairro,
            string logradouro,
            string numero,
            string complemento)
        {
            if (usuarioCodigo <= 0
                || TemCampoVazio(nome, cpf, email, cidade, estado, bairro, logradouro, numero))
            {
                MostrarAviso("Preencha todos os campos obrigatórios corretamente.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                // 1. Dados de acesso (usuario).
                const string sqlUsuario = @"
                    UPDATE usuario
                    SET nome = @nome, cpf = @cpf, email = @email
                    WHERE codigo = @codigo;";

                using (MySqlCommand cmd = new MySqlCommand(sqlUsuario, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@nome", nome.Trim()));
                    cmd.Parameters.Add(P("@cpf", cpf.Trim()));
                    cmd.Parameters.Add(P("@email", email.Trim()));
                    cmd.Parameters.Add(P("@codigo", usuarioCodigo));

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("O usuário não foi encontrado.");
                }

                // 2. Endereço (atualiza o existente ou cria um novo).
                long idEndereco = SalvarEndereco(
                    transacao, enderecoCodigo, cidade, estado, bairro, logradouro, numero, complemento);

                // 3. Dados específicos do professor.
                const string sqlProf = @"
                    UPDATE professor
                    SET titulacao = @titulacao,
                        especialidade = @especialidade,
                        enderecoCodigo = @enderecoCodigo
                    WHERE usuarioCodigo = @usuarioCodigo;";

                using (MySqlCommand cmd = new MySqlCommand(sqlProf, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@titulacao", TextoOuNulo(titulacao)));
                    cmd.Parameters.Add(P("@especialidade", TextoOuNulo(especialidade)));
                    cmd.Parameters.Add(P("@enderecoCodigo", idEndereco));
                    cmd.Parameters.Add(P("@usuarioCodigo", usuarioCodigo));

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("O professor não foi encontrado.");
                }

                transacao.Commit();
                return true;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveDuplicada)
            {
                Reverter(transacao);
                MostrarAviso("Já existe outro usuário com esse CPF ou e-mail.", "Cadastro Duplicado");
                return false;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao atualizar professor:\n" + ex.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>
        /// Exclui o usuário do professor e o endereço associado.
        /// Pressupõe ON DELETE CASCADE de usuario -> professor.
        /// </summary>
        public bool ExcluirProfessor(int usuarioCodigo, int? enderecoCodigo)
        {
            if (usuarioCodigo <= 0)
            {
                MostrarAviso("Selecione um professor válido para excluir.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM usuario WHERE codigo = @codigo;", conexao, transacao))
                {
                    cmd.Parameters.Add(P("@codigo", usuarioCodigo));

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("O professor não foi encontrado.");
                }

                // O endereço só pode ser removido depois do professor que o referencia.
                if (enderecoCodigo.HasValue && enderecoCodigo.Value > 0)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE FROM endereco WHERE codigo = @codigo;", conexao, transacao))
                    {
                        cmd.Parameters.Add(P("@codigo", enderecoCodigo.Value));
                        cmd.ExecuteNonQuery();
                    }
                }

                transacao.Commit();
                return true;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveEstrangeira)
            {
                Reverter(transacao);
                MostrarAviso(
                    "Não é possível excluir este professor porque ele está vinculado a turmas.\n\n" +
                    "Desvincule o professor das turmas antes de excluí-lo.",
                    "Exclusão Impedida");
                return false;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao tentar excluir o professor:\n" + ex.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        // ==================================================================
        // IDIOMAS
        // ==================================================================

        /// <summary>
        /// Lista idiomas para preencher ComboBox (colunas em minúsculo: codigo, nome).
        /// Chamadas sem argumento usam esta versão; para filtrar, use ListarIdiomas(filtro).
        /// </summary>
        public DataTable ListarIdiomas()
        {
            return Consultar("SELECT codigo, nome FROM idioma ORDER BY nome;", "Erro ao listar idiomas");
        }

        /// <summary>Lista idiomas para grid, com filtro por nome (colunas Codigo, Nome).</summary>
        public DataTable ListarIdiomas(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();

            return Consultar(
                "SELECT codigo AS Codigo, nome AS Nome FROM idioma WHERE nome LIKE @filtro ORDER BY nome;",
                "Erro ao consultar idiomas",
                P("@filtro", "%" + filtro + "%"));
        }

        /// <summary>Cadastra um novo idioma (ex.: Inglês, Espanhol).</summary>
        public bool CadastroIdiomas(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                MostrarAviso("Informe o nome do idioma.", "Campo Obrigatório");
                return false;
            }

            return ExecutarUnico(
                "INSERT INTO idioma (nome) VALUES (@nome);",
                "Erro ao cadastrar idioma",
                "Este idioma já está cadastrado no sistema.",
                null,
                P("@nome", nome.Trim()));
        }

        /// <summary>Mantido por compatibilidade: apenas chama CadastroIdiomas.</summary>
        public bool CadastrarIdioma(string nome)
        {
            return CadastroIdiomas(nome);
        }

        /// <summary>Atualiza o nome de um idioma.</summary>
        public bool AtualizarIdioma(int codigo, string nome)
        {
            if (codigo <= 0 || string.IsNullOrWhiteSpace(nome))
            {
                MostrarAviso("Selecione um idioma e informe um nome válido.");
                return false;
            }

            return ExecutarUnico(
                "UPDATE idioma SET nome = @nome WHERE codigo = @codigo;",
                "Erro ao atualizar idioma",
                "Já existe outro idioma cadastrado com esse nome.",
                null,
                P("@nome", nome.Trim()),
                P("@codigo", codigo));
        }

        /// <summary>Exclui um idioma (bloqueado se estiver em uso por cursos).</summary>
        public bool ExcluirIdioma(int codigo)
        {
            if (codigo <= 0)
            {
                MostrarAviso("Selecione um idioma válido para excluir.");
                return false;
            }

            return ExecutarUnico(
                "DELETE FROM idioma WHERE codigo = @codigo;",
                "Erro ao excluir idioma",
                null,
                "Não é possível excluir este idioma porque existem cursos associados a ele.\n\n" +
                "Remova os vínculos antes de tentar excluí-lo.",
                P("@codigo", codigo));
        }

        // ==================================================================
        // CURSOS
        // ==================================================================

        /// <summary>Lista cursos (com o nome do idioma) filtrando por curso ou idioma.</summary>
        public DataTable ListarCursos(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();

            const string sql = @"
                SELECT
                    c.codigo       AS Codigo,
                    c.nome         AS Nome,
                    c.idiomaCodigo AS IdiomaCodigo,
                    i.nome         AS Idioma,
                    c.cargaHoraria AS CargaHoraria,
                    c.valor        AS Valor
                FROM curso c
                INNER JOIN idioma i ON c.idiomaCodigo = i.codigo
                WHERE c.nome LIKE @filtro OR i.nome LIKE @filtro
                ORDER BY c.nome;";

            return Consultar(sql, "Erro ao consultar cursos", P("@filtro", "%" + filtro + "%"));
        }

        /// <summary>Cadastra um curso.</summary>
        public bool CadastrarCurso(string nome, int idiomaCodigo, int cargaHoraria, decimal valor)
        {
            if (string.IsNullOrWhiteSpace(nome) || idiomaCodigo <= 0 || cargaHoraria <= 0 || valor < 0)
            {
                MostrarAviso("Preencha todos os campos obrigatórios corretamente.");
                return false;
            }

            const string sql = @"
                INSERT INTO curso (nome, idiomaCodigo, cargaHoraria, valor)
                VALUES (@nome, @idiomaCodigo, @cargaHoraria, @valor);";

            return ExecutarUnico(sql, "Erro ao cadastrar curso", null, null,
                P("@nome", nome.Trim()),
                P("@idiomaCodigo", idiomaCodigo),
                P("@cargaHoraria", cargaHoraria),
                P("@valor", valor));
        }

        /// <summary>Atualiza um curso.</summary>
        public bool AtualizarCurso(int codigo, string nome, int idiomaCodigo, int cargaHoraria, decimal valor)
        {
            if (codigo <= 0 || string.IsNullOrWhiteSpace(nome)
                || idiomaCodigo <= 0 || cargaHoraria <= 0 || valor < 0)
            {
                MostrarAviso("Preencha todos os campos corretamente.");
                return false;
            }

            const string sql = @"
                UPDATE curso
                SET nome = @nome,
                    idiomaCodigo = @idiomaCodigo,
                    cargaHoraria = @cargaHoraria,
                    valor = @valor
                WHERE codigo = @codigo;";

            return ExecutarUnico(sql, "Erro ao atualizar curso", null, null,
                P("@nome", nome.Trim()),
                P("@idiomaCodigo", idiomaCodigo),
                P("@cargaHoraria", cargaHoraria),
                P("@valor", valor),
                P("@codigo", codigo));
        }

        /// <summary>Exclui um curso (bloqueado se houver turmas ou avaliações).</summary>
        public bool ExcluirCurso(int codigo)
        {
            if (codigo <= 0)
                return false;

            return ExecutarUnico(
                "DELETE FROM curso WHERE codigo = @codigo;",
                "Erro ao excluir curso",
                null,
                "Não é possível excluir este curso pois existem turmas ou avaliações vinculadas a ele.",
                P("@codigo", codigo));
        }

        // ==================================================================
        // TURMAS
        // ==================================================================

        /// <summary>Lista turmas com o curso e o professor responsável.</summary>
        public DataTable ListarTurmas(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();

            const string sql = @"
                SELECT
                    t.codigo        AS Codigo,
                    t.cursoCodigo   AS CursoCodigo,
                    c.nome          AS Curso,
                    t.horarioInicio AS HorarioInicio,
                    t.horarioFim    AS HorarioFim,
                    p.usuarioCodigo AS ProfessorCodigo,
                    u.nome          AS Professor
                FROM turma t
                INNER JOIN curso c ON t.cursoCodigo = c.codigo
                LEFT JOIN lecionar l ON t.codigo = l.turmaCodigo
                LEFT JOIN professor p ON l.professorCodigo = p.usuarioCodigo
                LEFT JOIN usuario u ON p.usuarioCodigo = u.codigo
                WHERE c.nome LIKE @filtro OR u.nome LIKE @filtro
                ORDER BY t.codigo DESC;";

            return Consultar(sql, "Erro ao listar turmas", P("@filtro", "%" + filtro + "%"));
        }

        /// <summary>Cadastra a turma e, se informado, o professor na tabela lecionar.</summary>
        public bool CadastrarTurma(int cursoCodigo, TimeSpan horarioInicio, TimeSpan horarioFim, int? professorCodigo)
        {
            if (cursoCodigo <= 0)
            {
                MostrarAviso("Selecione um curso válido.");
                return false;
            }

            if (horarioFim <= horarioInicio)
            {
                MostrarAviso("O horário de término deve ser posterior ao de início.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                const string sqlTurma = @"
                    INSERT INTO turma (cursoCodigo, horarioInicio, horarioFim)
                    VALUES (@cursoCodigo, @horarioInicio, @horarioFim);";

                long idTurma;

                using (MySqlCommand cmd = new MySqlCommand(sqlTurma, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@cursoCodigo", cursoCodigo));
                    cmd.Parameters.Add(P("@horarioInicio", horarioInicio));
                    cmd.Parameters.Add(P("@horarioFim", horarioFim));
                    cmd.ExecuteNonQuery();
                    idTurma = cmd.LastInsertedId;
                }

                VincularProfessor(transacao, idTurma, professorCodigo);

                transacao.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao cadastrar turma:\n" + ex.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>Atualiza curso e horários da turma e renova o vínculo com o professor.</summary>
        public bool AtualizarTurma(int turmaCodigo, int cursoCodigo, TimeSpan horarioInicio, TimeSpan horarioFim, int? professorCodigo)
        {
            if (turmaCodigo <= 0 || cursoCodigo <= 0)
                return false;

            if (horarioFim <= horarioInicio)
            {
                MostrarAviso("O horário de término deve ser posterior ao de início.");
                return false;
            }

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                const string sqlTurma = @"
                    UPDATE turma
                    SET cursoCodigo = @cursoCodigo,
                        horarioInicio = @horarioInicio,
                        horarioFim = @horarioFim
                    WHERE codigo = @codigo;";

                using (MySqlCommand cmd = new MySqlCommand(sqlTurma, conexao, transacao))
                {
                    cmd.Parameters.Add(P("@cursoCodigo", cursoCodigo));
                    cmd.Parameters.Add(P("@horarioInicio", horarioInicio));
                    cmd.Parameters.Add(P("@horarioFim", horarioFim));
                    cmd.Parameters.Add(P("@codigo", turmaCodigo));

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("A turma não foi encontrada.");
                }

                // Remove a atribuição anterior e grava a nova.
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM lecionar WHERE turmaCodigo = @turmaCodigo;", conexao, transacao))
                {
                    cmd.Parameters.Add(P("@turmaCodigo", turmaCodigo));
                    cmd.ExecuteNonQuery();
                }

                VincularProfessor(transacao, turmaCodigo, professorCodigo);

                transacao.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao atualizar turma:\n" + ex.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        /// <summary>
        /// Exclui a turma e o vínculo com o professor.
        /// Bloqueada se houver matrículas ou avaliações.
        /// </summary>
        public bool ExcluirTurma(int turmaCodigo)
        {
            if (turmaCodigo <= 0)
                return false;

            if (!Conectar())
                return false;

            MySqlTransaction transacao = null;

            try
            {
                transacao = conexao.BeginTransaction();

                // O vínculo com o professor é removido antes; assim, um erro de FK
                // no DELETE da turma significa de fato matrículas/avaliações.
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM lecionar WHERE turmaCodigo = @codigo;", conexao, transacao))
                {
                    cmd.Parameters.Add(P("@codigo", turmaCodigo));
                    cmd.ExecuteNonQuery();
                }

                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM turma WHERE codigo = @codigo;", conexao, transacao))
                {
                    cmd.Parameters.Add(P("@codigo", turmaCodigo));

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("A turma não foi encontrada.");
                }

                transacao.Commit();
                return true;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveEstrangeira)
            {
                Reverter(transacao);
                MostrarAviso(
                    "Não é possível excluir esta turma pois existem alunos matriculados ou avaliações vinculadas.",
                    "Exclusão Impedida");
                return false;
            }
            catch (Exception ex)
            {
                Reverter(transacao);
                MostrarErro("Erro ao excluir turma:\n" + ex.Message);
                return false;
            }
            finally
            {
                if (transacao != null)
                    transacao.Dispose();

                Desconectar();
            }
        }

        // ==================================================================
        // MATRÍCULAS
        // ==================================================================

        /// <summary>Lista matrículas com aluno, curso e horário da turma.</summary>
        public DataTable ListarMatriculas(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();

            const string sql = @"
                SELECT
                    m.codigo        AS Codigo,
                    m.alunoCodigo   AS AlunoCodigo,
                    a.nome          AS Aluno,
                    m.turmaCodigo   AS TurmaCodigo,
                    c.nome          AS Curso,
                    t.horarioInicio AS HorarioInicio,
                    t.horarioFim    AS HorarioFim
                FROM matricular m
                INNER JOIN aluno a ON m.alunoCodigo = a.codigo
                INNER JOIN turma t ON m.turmaCodigo = t.codigo
                INNER JOIN curso c ON t.cursoCodigo = c.codigo
                WHERE a.nome LIKE @filtro OR c.nome LIKE @filtro
                ORDER BY m.codigo DESC;";

            return Consultar(sql, "Erro ao consultar matrículas", P("@filtro", "%" + filtro + "%"));
        }

        /// <summary>Matricula um aluno em uma turma.</summary>
        public bool RealizarMatricula(int alunoCodigo, int turmaCodigo)
        {
            if (alunoCodigo <= 0 || turmaCodigo <= 0)
            {
                MostrarAviso("Selecione um aluno e uma turma válidos.");
                return false;
            }

            return ExecutarUnico(
                "INSERT INTO matricular (alunoCodigo, turmaCodigo) VALUES (@alunoCodigo, @turmaCodigo);",
                "Erro ao efetuar matrícula",
                "Este aluno já está matriculado nesta turma.",
                null,
                P("@alunoCodigo", alunoCodigo),
                P("@turmaCodigo", turmaCodigo));
        }

        /// <summary>Cancela uma matrícula (bloqueado se houver avaliações lançadas).</summary>
        public bool CancelarMatricula(int matriculaCodigo)
        {
            if (matriculaCodigo <= 0)
            {
                MostrarAviso("Selecione uma matrícula válida para cancelar.");
                return false;
            }

            return ExecutarUnico(
                "DELETE FROM matricular WHERE codigo = @codigo;",
                "Erro ao cancelar matrícula",
                null,
                "Não é possível cancelar esta matrícula pois o aluno já possui notas ou avaliações registradas.\n\n" +
                "Remova os lançamentos de avaliações antes de cancelar a matrícula.",
                P("@codigo", matriculaCodigo));
        }

        // ==================================================================
        // AVALIAÇÕES E HISTÓRICO (CORRIGIDOS)
        // ==================================================================

        /// <summary>Histórico de avaliações de um aluno específico pelo código.</summary>
        public DataTable ObterHistoricoAluno(int alunoCodigo)
        {
            const string sql = @"
        SELECT
            av.codigo        AS Codigo,
            a.nome           AS Aluno,
            a.cpf            AS CPF,
            c.nome           AS Curso,
            av.turmaCodigo   AS Turma,
            av.nota          AS Nota,
            av.dataAvaliacao AS Data,
            av.observacao    AS Observacao
        FROM avaliacao av
        INNER JOIN aluno a ON av.alunoCodigo = a.codigo
        INNER JOIN curso c ON av.cursoCodigo = c.codigo
        WHERE av.alunoCodigo = @alunoCodigo
        ORDER BY av.dataAvaliacao DESC;";

            return Consultar(sql, "Erro ao carregar histórico", P("@alunoCodigo", alunoCodigo));
        }

        /// <summary>Histórico de avaliações filtrando por nome ou CPF do aluno (ou traz todos se vazio).</summary>
        public DataTable ObterHistoricoAlunoPorFiltro(string filtro)
        {
            filtro = (filtro ?? string.Empty).Trim();
            string digitos = SomenteDigitos(filtro);

            // Se o filtro estiver em branco, traz o histórico de todos os alunos cadastrados
            string condicaoWhere = string.IsNullOrWhiteSpace(filtro)
                ? string.Empty
                : " WHERE (a.nome LIKE @filtro" + CondicaoCpf("a.cpf", digitos) + ") ";

            string sql = $@"
        SELECT
            av.codigo        AS Codigo,
            a.nome           AS Aluno,
            a.cpf            AS CPF,
            c.nome           AS Curso,
            av.turmaCodigo   AS Turma,
            av.nota          AS Nota,
            av.dataAvaliacao AS Data,
            av.observacao    AS Observacao
        FROM avaliacao av
        INNER JOIN aluno a ON av.alunoCodigo = a.codigo
        INNER JOIN curso c ON av.cursoCodigo = c.codigo
        {condicaoWhere}
        ORDER BY av.dataAvaliacao DESC;";

            return Consultar(sql, "Erro ao carregar histórico do aluno", ParametrosBusca(filtro, digitos));
        }

        /// <summary>Lista avaliações filtrando por nome do aluno ou do curso.</summary>
        public DataTable ListarAvaliacoes(string filtro = "")
        {
            filtro = (filtro ?? string.Empty).Trim();

            const string sql = @"
                SELECT
                    av.codigo        AS Codigo,
                    av.alunoCodigo   AS AlunoCodigo,
                    a.nome           AS Aluno,
                    av.cursoCodigo   AS CursoCodigo,
                    c.nome           AS Curso,
                    av.turmaCodigo   AS TurmaCodigo,
                    av.nota          AS Nota,
                    av.observacao    AS Observacao,
                    av.dataAvaliacao AS Data
                FROM avaliacao av
                INNER JOIN aluno a ON av.alunoCodigo = a.codigo
                INNER JOIN curso c ON av.cursoCodigo = c.codigo
                INNER JOIN turma t ON av.turmaCodigo = t.codigo
                WHERE a.nome LIKE @filtro OR c.nome LIKE @filtro
                ORDER BY av.codigo DESC;";

            return Consultar(sql, "Erro ao listar avaliações", P("@filtro", "%" + filtro + "%"));
        }

        /// <summary>Cadastra uma avaliação (nota de 0 a 10).</summary>
        public bool CadastrarAvaliacao(int alunoCodigo, int cursoCodigo, int turmaCodigo, decimal nota, string observacao, DateTime dataAvaliacao)
        {
            if (alunoCodigo <= 0 || cursoCodigo <= 0 || turmaCodigo <= 0 || nota < 0 || nota > 10)
            {
                MostrarAviso("Informe aluno, curso, turma e uma nota válida (0 a 10).");
                return false;
            }

            const string sql = @"
                INSERT INTO avaliacao
                    (nota, dataAvaliacao, observacao, cursoCodigo, turmaCodigo, alunoCodigo)
                VALUES
                    (@nota, @dataAvaliacao, @observacao, @cursoCodigo, @turmaCodigo, @alunoCodigo);";

            return ExecutarUnico(sql, "Erro ao cadastrar avaliação", null, null,
                P("@nota", nota),
                P("@dataAvaliacao", dataAvaliacao),
                P("@observacao", TextoOuNulo(observacao)),
                P("@cursoCodigo", cursoCodigo),
                P("@turmaCodigo", turmaCodigo),
                P("@alunoCodigo", alunoCodigo));
        }

        /// <summary>Atualiza nota, observação e data de uma avaliação.</summary>
        public bool AtualizarAvaliacao(int codigo, decimal nota, string observacao, DateTime dataAvaliacao)
        {
            if (codigo <= 0 || nota < 0 || nota > 10)
            {
                MostrarAviso("Informe uma nota válida (0 a 10).");
                return false;
            }

            const string sql = @"
                UPDATE avaliacao
                SET nota = @nota,
                    observacao = @observacao,
                    dataAvaliacao = @dataAvaliacao
                WHERE codigo = @codigo;";

            return ExecutarUnico(sql, "Erro ao atualizar avaliação", null, null,
                P("@nota", nota),
                P("@observacao", TextoOuNulo(observacao)),
                P("@dataAvaliacao", dataAvaliacao),
                P("@codigo", codigo));
        }

        /// <summary>Exclui uma avaliação pelo código.</summary>
        public bool ExcluirAvaliacao(int codigo)
        {
            if (codigo <= 0)
                return false;

            return ExecutarUnico(
                "DELETE FROM avaliacao WHERE codigo = @codigo;",
                "Erro ao excluir avaliação", null, null,
                P("@codigo", codigo));
        }

        // ==================================================================
        // MÉTODOS AUXILIARES (privados)
        // ==================================================================

        // ---------- Acesso ao banco ----------

        /// <summary>Cria um parâmetro SQL; valores nulos viram NULL do banco.</summary>
        private static MySqlParameter P(string nome, object valor)
        {
            return new MySqlParameter(nome, valor ?? DBNull.Value);
        }

        /// <summary>Executa um SELECT e devolve o resultado em um DataTable (vazio em caso de erro).</summary>
        private DataTable Consultar(string sql, string tituloErro, params MySqlParameter[] parametros)
        {
            DataTable tabela = new DataTable();

            if (!Conectar())
                return tabela;

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                {
                    cmd.Parameters.AddRange(parametros);

                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(tabela);
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarErro(tituloErro + ":\n" + ex.Message);
            }
            finally
            {
                Desconectar();
            }

            return tabela;
        }

        /// <summary>
        /// Executa um INSERT/UPDATE/DELETE de comando único.
        /// Retorna true se exatamente uma linha foi afetada.
        /// Mensagens opcionais (null = tratamento genérico) para duplicidade e registro em uso.
        /// </summary>
        private bool ExecutarUnico(
            string sql,
            string erroGenerico,
            string msgDuplicado,
            string msgEmUso,
            params MySqlParameter[] parametros)
        {
            if (!Conectar())
                return false;

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                {
                    cmd.Parameters.AddRange(parametros);
                    return cmd.ExecuteNonQuery() == 1;
                }
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveDuplicada && msgDuplicado != null)
            {
                MostrarAviso(msgDuplicado, "Cadastro Duplicado");
                return false;
            }
            catch (MySqlException ex) when (ex.Number == ErroChaveEstrangeira && msgEmUso != null)
            {
                MostrarAviso(msgEmUso, "Operação Impedida");
                return false;
            }
            catch (Exception ex)
            {
                MostrarErro(erroGenerico + ":\n" + ex.Message);
                return false;
            }
            finally
            {
                Desconectar();
            }
        }

        /// <summary>Insere um endereço dentro da transação e devolve o código gerado.</summary>
        private long InserirEndereco(
            MySqlTransaction transacao,
            string cidade, string estado, string bairro,
            string logradouro, string numero, string complemento)
        {
            const string sql = @"
                INSERT INTO endereco
                    (cidade, estado, bairro, logradouro, numero, complemento)
                VALUES
                    (@cidade, @estado, @bairro, @logradouro, @numero, @complemento);";

            using (MySqlCommand cmd = new MySqlCommand(sql, conexao, transacao))
            {
                PreencherParametrosEndereco(cmd, cidade, estado, bairro, logradouro, numero, complemento);
                cmd.ExecuteNonQuery();
                return cmd.LastInsertedId;
            }
        }

        /// <summary>
        /// Atualiza o endereço existente ou cria um novo (quando enderecoCodigo é nulo).
        /// Devolve o código do endereço salvo.
        /// </summary>
        private long SalvarEndereco(
            MySqlTransaction transacao, int? enderecoCodigo,
            string cidade, string estado, string bairro,
            string logradouro, string numero, string complemento)
        {
            if (!enderecoCodigo.HasValue)
                return InserirEndereco(transacao, cidade, estado, bairro, logradouro, numero, complemento);

            const string sql = @"
                UPDATE endereco
                SET cidade = @cidade,
                    estado = @estado,
                    bairro = @bairro,
                    logradouro = @logradouro,
                    numero = @numero,
                    complemento = @complemento
                WHERE codigo = @codigo;";

            using (MySqlCommand cmd = new MySqlCommand(sql, conexao, transacao))
            {
                PreencherParametrosEndereco(cmd, cidade, estado, bairro, logradouro, numero, complemento);
                cmd.Parameters.Add(P("@codigo", enderecoCodigo.Value));
                cmd.ExecuteNonQuery();
            }

            return enderecoCodigo.Value;
        }

        /// <summary>Insere o registro de professor dentro da transação.</summary>
        private void InserirProfessor(
            MySqlTransaction transacao, long usuarioCodigo,
            string titulacao, string especialidade, long enderecoCodigo)
        {
            const string sql = @"
                INSERT INTO professor
                    (usuarioCodigo, titulacao, especialidade, dataAdmissao, enderecoCodigo)
                VALUES
                    (@usuarioCodigo, @titulacao, @especialidade, @dataAdmissao, @enderecoCodigo);";

            using (MySqlCommand cmd = new MySqlCommand(sql, conexao, transacao))
            {
                cmd.Parameters.Add(P("@usuarioCodigo", usuarioCodigo));
                cmd.Parameters.Add(P("@titulacao", TextoOuNulo(titulacao)));
                cmd.Parameters.Add(P("@especialidade", TextoOuNulo(especialidade)));
                cmd.Parameters.Add(P("@dataAdmissao", DateTime.Today));
                cmd.Parameters.Add(P("@enderecoCodigo", enderecoCodigo));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Grava o vínculo turma-professor na tabela lecionar (se houver professor).</summary>
        private void VincularProfessor(MySqlTransaction transacao, long turmaCodigo, int? professorCodigo)
        {
            if (!professorCodigo.HasValue || professorCodigo.Value <= 0)
                return;

            const string sql = @"
                INSERT INTO lecionar (turmaCodigo, professorCodigo)
                VALUES (@turmaCodigo, @professorCodigo);";

            using (MySqlCommand cmd = new MySqlCommand(sql, conexao, transacao))
            {
                cmd.Parameters.Add(P("@turmaCodigo", turmaCodigo));
                cmd.Parameters.Add(P("@professorCodigo", professorCodigo.Value));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Preenche os parâmetros de endereço usados no INSERT e no UPDATE.</summary>
        private static void PreencherParametrosEndereco(
            MySqlCommand cmd,
            string cidade, string estado, string bairro,
            string logradouro, string numero, string complemento)
        {
            // A coluna endereco.estado é VARCHAR(2): exige a sigla da UF (ex.: SP).
            if (estado.Trim().Length != 2)
                throw new ArgumentException("O estado deve ser informado com a sigla de 2 letras (ex.: SP).");

            cmd.Parameters.Add(P("@cidade", cidade.Trim()));
            cmd.Parameters.Add(P("@estado", estado.Trim().ToUpperInvariant()));
            cmd.Parameters.Add(P("@bairro", bairro.Trim()));
            cmd.Parameters.Add(P("@logradouro", logradouro.Trim()));
            cmd.Parameters.Add(P("@numero", numero.Trim()));
            cmd.Parameters.Add(P("@complemento", TextoOuNulo(complemento)));
        }

        /// <summary>Desfaz a transação sem lançar exceção (a conexão pode já ter caído).</summary>
        private static void Reverter(MySqlTransaction transacao)
        {
            if (transacao == null)
                return;

            try
            {
                transacao.Rollback();
            }
            catch (Exception)
            {
                // Se a conexão foi perdida, o MySQL já descarta a transação.
            }
        }

        // ---------- Filtros de busca (nome ou CPF) ----------

        /// <summary>Expressão SQL que remove pontos, traço e espaços do CPF gravado no banco.</summary>
        private static string ExprCpf(string coluna)
        {
            return "REPLACE(REPLACE(REPLACE(" + coluna + ",'.',''),'-',''),' ','')";
        }

        /// <summary>
        /// Trecho " OR cpf LIKE @cpf" — só existe quando o filtro tem dígitos.
        /// Sem isso, o LIKE '%%' casaria com todos os registros.
        /// </summary>
        private static string CondicaoCpf(string coluna, string digitos)
        {
            return digitos.Length > 0 ? " OR " + ExprCpf(coluna) + " LIKE @cpf" : string.Empty;
        }

        /// <summary>Parâmetros @filtro (e @cpf, quando houver dígitos) para as buscas.</summary>
        private static MySqlParameter[] ParametrosBusca(string filtro, string digitos)
        {
            List<MySqlParameter> lista = new List<MySqlParameter> { P("@filtro", "%" + filtro + "%") };

            if (digitos.Length > 0)
                lista.Add(P("@cpf", "%" + digitos + "%"));

            return lista.ToArray();
        }

        // ---------- Validação e formatação ----------

        /// <summary>Retorna true se algum dos textos for nulo, vazio ou só espaços.</summary>
        private static bool TemCampoVazio(params string[] campos)
        {
            return campos.Any(string.IsNullOrWhiteSpace);
        }

        /// <summary>Texto vazio vira NULL do banco; senão, devolve o texto aparado.</summary>
        private static object TextoOuNulo(string texto)
        {
            return string.IsNullOrWhiteSpace(texto) ? (object)DBNull.Value : texto.Trim();
        }

        /// <summary>Mantém apenas os dígitos (remove pontos, traços, parênteses e espaços).</summary>
        private static string SomenteDigitos(string texto)
        {
            return new string((texto ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        /// <summary>Normaliza o perfil (maiúsculo) e devolve null se não for ADMIN, SECRETARIA ou DOCENTE.</summary>
        private static string NormalizarPerfil(string perfil)
        {
            string p = (perfil ?? string.Empty).Trim().ToUpperInvariant();
            return PerfisValidos.Contains(p) ? p : null;
        }

        /// <summary>Data de nascimento não pode ser futura nem anterior a 1900.</summary>
        private static bool DataNascimentoValida(DateTime data)
        {
            return data.Date <= DateTime.Today && data.Year >= 1900;
        }

        // ---------- Mensagens ao usuário ----------

        private static void MostrarErro(string mensagem, string titulo = "Erro")
        {
            MessageBox.Show(mensagem, titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void MostrarAviso(string mensagem, string titulo = "Aviso")
        {
            MessageBox.Show(mensagem, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

    } // Fim da classe
} // Fim do projeto