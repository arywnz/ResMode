# ResMode 0.2.2-alpha.23

**Atualizador sem PowerShell.** Antes, ao atualizar, o app abria um PowerShell oculto com um comando codificado para rodar o instalador, o tipo de coisa que antivírus costumam desconfiar. Agora uma cópia do próprio ResMode, na pasta de dados do app, espera o app fechar, roda o instalador e abre o app de novo. Ela só aceita instaladores que o próprio app baixou para a pasta de atualizações. Testado de ponta a ponta no Windows com o MSI oficial.

**Log exportado mais limpo e com a placa de vídeo.** O log mostrava "Vídeo: não encontrado" porque procurava a placa com um identificador errado no registro; agora lista a placa e a versão do driver. Além do nome de usuário, o log passa a remover as pastas de outras contas do Windows, o nome do computador, e-mails e ids de conta.

**Logs recebidos em issues.** O repositório ganhou um modelo de issue que pede para colar o log num bloco de código (ou anexar só o .txt) e uma seção no `docs/SECURITY.md` sobre tratar logs recebidos como dados não confiáveis.

MSI para Windows x64, sem assinatura digital. Feche o ResMode antes de atualizar, quando nenhum jogo estiver aberto. As configurações são preservadas.
