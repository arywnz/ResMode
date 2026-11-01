# ResMode

Use uma resolução maior nos jogos sem precisar deixar o Windows assim o dia inteiro. O ResMode acompanha os jogos escolhidos, muda a resolução do monitor e volta ao normal quando você termina de jogar.

## Baixar

### [⬇ Baixar ResMode para Windows (.msi)](https://github.com/arywnz/ResMode/releases/download/v0.2.2-alpha.5/ResMode-0.2.2-alpha.5-win-x64.msi)

Para **Windows 10 ou 11, de 64 bits**. Esta é uma versão de testes. [Outras versões e arquivos](https://github.com/arywnz/ResMode/releases).

O aplicativo é gratuito e não exige comprar um certificado. O instalador ainda **não tem assinatura digital**, então alguns Windows podem mostrar um aviso ou bloquear a instalação ou a execução. Isso depende das proteções ativas em cada PC; o MSI não remove essa restrição.

## Como usar

1. Baixe o MSI e siga a instalação. Se já usa o ResMode, feche-o antes de atualizar, quando nenhum jogo estiver aberto.
2. Abra o ResMode pelo atalho e escolha o monitor e a resolução de jogo nas configurações.
3. Confira a biblioteca e marque os jogos que quer acompanhar. Você também pode adicionar um jogo manualmente.

A resolução escolhida precisa estar disponível no driver da sua placa de vídeo. O ResMode não cria resoluções que o monitor ou o driver não oferecem.

Enquanto o jogo estiver aberto, a área de trabalho também pode ficar na resolução de jogo, inclusive durante Alt+Tab. Ela volta ao normal ao encerrar a sessão. DLSS e outras opções de qualidade continuam nas configurações de cada jogo.

## O que você encontra no app

- Biblioteca organizada por launcher, com capas e opção de abrir o jogo.
- Jogos adicionados e removidos da biblioteca acompanhados automaticamente.
- Lista de jogos ignorados que continua salva depois de reiniciar o PC.
- Escolha do monitor, início com o Windows e registro de atividade, com um botão que gera o log em .txt (Documentos/ResMode) para enviar quando precisar de ajuda.
- Aviso quando sai uma versão nova no GitHub, com botão para baixar e instalar sem abrir o navegador. Dá para desligar em Configurações → Versão.
- Restauração da resolução e tentativa de manter as outras janelas no lugar.
- Opção de recuperar a nitidez da barra de tarefas ao voltar do jogo. Ela reinicia a interface do Explorer e fica desligada por padrão.

A função de conquistas foi removida. O ResMode não modifica arquivos dos jogos, não altera saves e não injeta código neles. O comportamento das janelas ainda pode variar conforme o jogo, o driver e os outros programas abertos.

## Seus dados

O ResMode não tem conta própria, anúncios nem envio automático de logs. A biblioteca, as preferências e o histórico ficam no seu PC e são protegidos com a criptografia do próprio Windows, ligada ao seu usuário. Não há uma senha ou chave compartilhada dentro do instalador.

Você pode instalar o app normalmente em outros PCs. Cada instalação cria seus próprios dados; copiar os arquivos protegidos de um usuário ou computador para outro não é uma forma de transferir as configurações. A proteção também não impede o acesso por outro programa que já esteja rodando com a sua conta do Windows.

Para identificar quais itens da Steam são jogos, o app pode consultar a loja com o número público do item. Para avisar de versões novas, ele consulta a lista de releases do GitHub (sem enviar nada seu) ao abrir e a cada 12 horas; o instalador só é executado se o SHA-256 baixado for o publicado no release. As capas e o tempo de jogo são lidos dos arquivos locais da Steam. [Veja os detalhes de privacidade](PRIVACY.md).

## Remover ou pedir ajuda

Para desinstalar o MSI, use **Configurações → Aplicativos → ResMode**. Feche o ResMode primeiro. Suas configurações ficam guardadas para uma futura reinstalação; para apagá-las também, remova a pasta `%LOCALAPPDATA%\ResMode` depois de fechar o app.

Encontrou um problema? [Abra uma issue](https://github.com/arywnz/ResMode/issues) contando o que aconteceu. Revise qualquer histórico antes de compartilhar: ele pode mostrar nomes de jogos e caminhos de pastas.

O código é aberto e usa a [licença MIT](LICENSE). As fontes Geist mantêm sua [própria licença](THIRD_PARTY_NOTICES.md). Para compilar ou contribuir, veja [CONTRIBUTING.md](CONTRIBUTING.md). Informações sobre [segurança](SECURITY.md) e [assinatura digital](CODE_SIGNING.md).
