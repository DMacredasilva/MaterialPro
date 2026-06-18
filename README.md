# MaterialPro Updates

Arquivos de atualizacao gerados em 2026-06-18 15:37.

Versao publicada: 2026.06.18.1537.

Inclui:
- modulo de impressoras corrigido
- status de atualizacao cliente/servidor
- modulo de acesso por usuario
- modulo de acesso remoto para suporte
- painel principal visualmente ajustado para rotina de loja de material de construcao
- update exe agora mira automaticamente em C:\Program Files\MaterialPro\Client ou C:\Program Files\MaterialPro\Server conforme o canal
- central de atualizacoes mostra versao correta, conexao cliente-servidor, banco de dados e botao administrativo para forcar update
- painel inicial mostra Cliente, Servidor, Rede/Banco e Administrador com acesso rapido para diagnosticar e forcar instalar/update
- corrigido erro de SelectedIndex em listas vazias para nao travar ao abrir modulos
- modulo Backup para administrador gerar ZIP com arquivos do sistema, configuracao do cliente e dump do banco MySQL
- central de atualizacoes responsiva, com versao instalada e versao publicada no GitHub sem texto sobreposto
- consulta de versao no GitHub com fallback para redes que bloqueiam a API
- botao Forcar instalar/update do cliente baixa o pacote mais novo do GitHub, fecha o MaterialPro e reinstala por cima

Cliente:
- updates/client/MaterialProClientUpdate.exe
- updates/client/update-package.zip

Servidor:
- updates/server/MaterialProServerUpdate.exe
- updates/server/update-package.zip

Para atualizar, coloque o exe e o update-package.zip correspondente na pasta de instalacao e execute o exe de update.
