# Bacen-v2

## Configuração do Arquivo `appsettings.json`

Para que o sistema funcione corretamente, é necessário configurar um arquivo chamado `appsettings.json` no diretório `Data`. Este arquivo contém informações essenciais para o ambiente de execução e autenticação nos serviços utilizados.

### Localização
- O arquivo deve estar na pasta: `Data/appsettings.json`.

### Estrutura do Arquivo
Abaixo está o modelo que deve ser seguido:

```json
{
  "Environment": "Production", // Altere para "Production" ao usar o ambiente de produção (SANDBOX FORA DE FUNCIONAMENTO)
  "ServiceDesk": {
    "SandboxUrl": "https://servicedesksandbox.openfinancebrasil.org.br",
    "ProductionUrl": "https://servicedesk.openfinancebrasil.org.br",
    "Username": "seu_usuario@sicoob.com.br",
    "Password": "sua_senha",
    "userID": "seu_userID"
  },
  "TopDesk": {
    "BaseUrl": "https://atendimento.sisbr.coop.br",
    "Username": "seu_usuario",
    "Password": "sua_senha"
  }
}
```
### Detalhes dos Campos

- **Environment**: Define o ambiente do sistema. Utilize `"Sandbox"` para testes e `"Production"` para produção.
- **ServiceDesk**:
  - **SandboxUrl**: URL do ambiente de testes (sandbox).
  - **ProductionUrl**: URL do ambiente de produção.
  - **Username**: E-mail do usuário para autenticação.
  - **Password**: Senha do usuário para autenticação.
  - **userID**: Identificador do usuário no Service Desk.
- **TopDesk**:
  - **BaseUrl**: URL base para acesso ao TopDesk.
  - **Username**: Nome de usuário para autenticação.
  - **Password**: Senha para autenticação.

### Observações

- **Credenciais Sensíveis**: Não compartilhe este arquivo publicamente ou em repositórios públicos.
- **Ambiente de Produção**: Certifique-se de alterar o campo `"Environment"` para `"Production"` ao usar o ambiente de produção.
