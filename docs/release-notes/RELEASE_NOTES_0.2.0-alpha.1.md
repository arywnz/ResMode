# ResMode 0.2.0-alpha.1

- Instalador MSI para Windows x64, com instalação por usuário, atalhos e remoção pelas Configurações do Windows.
- Conquistas removidas do painel dos jogos e da leitura de dados da Steam.
- Preferências, catálogo, recuperação de janelas e histórico protegidos pela criptografia do Windows, sem chave compartilhada no app.
- Compatibilidade com configurações antigas: a leitura continua disponível; as novas gravações usam o formato protegido. O histórico antigo é migrado quando o aplicativo escreve nele.
- Histórico aberto dentro do app, sem criar uma cópia descriptografada na pasta temporária.
- README simplificado, com download direto do MSI.

Cada usuário/PC cria seus próprios dados. Arquivos protegidos não são portáteis para outra conta ou computador, e versões anteriores não conseguem ler o novo formato. O MSI preserva as configurações ao desinstalar. Em atualizações, feche o ResMode antes de iniciar o instalador, quando nenhum jogo estiver aberto.

Esta é uma pré-release **sem assinatura digital**. Não exige certificado pago nem conta ResMode, mas o Windows pode mostrar avisos ou bloquear programas sem assinatura. O MSI não elimina esse bloqueio. A integração SignPath não está ativa.

Os testes de CI verificam criptografia, migração, recuperação em processo auxiliar, preferências, fontes, cores, hover e instalação/desinstalação do MSI. Eles não substituem testes de jogos reais ou todos os cenários de múltiplos monitores.
