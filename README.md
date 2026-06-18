# MaterialPro

Base inicial do MaterialPro.

Entradas prontas:
- autenticação local
- banco MySQL
- usuário admin inicial
- instaladores de servidor e cliente
- script de publish dos setup
- clientes, fornecedores, produtos e estoque
- orçamento, vendas/PDV e caixa
- financeiro, contas a pagar, duplicatas e baixa
- relatórios PDF/Excel e impressão básica
- instalador servidor com backup, updater e diagnóstico interno
- instalador cliente com updater, diagnóstico e payload automático
- módulo Sistema > Importação > Arquivos DBF
- assistente de importação DBF com backup, validação e log
- cadastro editável de nome do programa, loja, CNPJ, endereço, telefone e logo
- nota avulsa nao fiscal com itens, PDF e controle interno
- modulo de seguranca com auditoria, bloqueio de login, sessoes e troca de senha
- documentos internos: cupom, recibo, orcamento, comprovante, segunda via e impressao 58mm, 80mm e A4
- documentos sempre internos do MaterialPro, sem NFC-e, NF-e, SAT, CF-e, SEFAZ, Receita Federal, certificado digital, token CSC ou configuracao fiscal
- cancelamento de vendas com status CANCELADA, motivo, senha de gerente, usuario, data/hora, estorno de estoque, cancelamento financeiro, log, comprovante e relatorio por periodo

Publicação dos instaladores:
- script: `build/publish-installers.ps1`
- saída esperada:
  - `dist/server/MaterialProServerSetup.exe`
  - `dist/client/MaterialProClientSetup.exe`

Admin padrão:
- usuário: `admin`
- senha: `Admin@123`
