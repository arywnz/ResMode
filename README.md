# ResMode

[![Build and test](https://github.com/arywnz/ResMode/actions/workflows/build.yml/badge.svg)](https://github.com/arywnz/ResMode/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Resolução de jogo automática, desktop na sua resolução normal.**

ResMode é um aplicativo Windows que muda a resolução do monitor escolhido ao detectar um jogo e restaura a resolução do desktop quando a sessão termina. Inclui biblioteca de jogos, inicialização pelo app, exclusões persistentes, configurações e ícone na bandeja.

**Automatic per-game display resolution switching for Windows.** ResMode uses display modes already offered by your graphics driver, with desktop restoration, launcher discovery and a local game library. No game injection, drivers or background Windows service.

> Pré-release em desenvolvimento. As primeiras distribuições são **sem assinatura confiável** e podem ser bloqueadas pelo Smart App Control. A solicitação à SignPath Foundation está em preparação; não há aprovação ou assinatura de produção ativa. Veja [Code signing policy](CODE_SIGNING.md).

## Download e instalação

Use a [página de releases](https://github.com/arywnz/ResMode/releases). Extraia o ZIP completo e execute `Instalar.cmd`. O pacote contém `dist/ResMode.exe`; o instalador usa esse arquivo sem recompilá-lo, preservando uma eventual assinatura digital. A versão unsigned é identificada no nome do ZIP e nas notas da release.

O instalador valida o executável, instala em `%LOCALAPPDATA%\ResMode`, cria o atalho na área de trabalho e configura o início com o Windows na primeira instalação. Não exige administrador. Se a política do Windows bloquear o executável, os autotestes interrompem a instalação antes de substituir a cópia instalada. Não desative proteções para instalar uma versão de teste.

Para remover o aplicativo e seus dados locais, execute `Desinstalar.cmd`. A desinstalação restaura o desktop e encerra somente o ResMode. Faça uma cópia das configurações antes, se quiser mantê-las.

## Requisitos

- Windows 10 ou 11, x64, com .NET Framework 4.8.
- Resolução desejada já disponível no driver: DSR/DLDSR da NVIDIA, VSR da AMD ou resolução personalizada compatível.
- Um modo de resolução maior não é criado pelo ResMode. O app apenas seleciona modos que o Windows e o driver oferecem.

Jogos sem bordas frequentemente usam a resolução do desktop. Por isso, o desktop também muda enquanto o jogo está aberto, inclusive durante Alt+Tab; ele retorna ao modo anterior ao encerrar a sessão. Qualidade, DLSS e escala de renderização continuam nas configurações do jogo.

## Funcionalidades

- Biblioteca por launcher, com detecção de instalações e remoções e adição manual.
- Capas locais, inicialização de jogos e painel de detalhes com tempo de jogo e conquistas disponíveis no cache local da Steam.
- Exclusão persistente de jogos do monitoramento; competitivos são ignorados por padrão.
- Seleção do monitor e da resolução de jogo; detecção em segundo plano.
- Restauração do desktop e tentativa de preservar a posição das janelas em múltiplos monitores.
- Recuperação da nitidez da barra de tarefas, quando habilitada. Essa opção pode reiniciar a interface do Explorer; janelas de pastas são tratadas pelo mecanismo de restauração.
- Processo auxiliar `/guard` para recuperar o desktop caso o processo principal encerre inesperadamente.
- Interface escura, animações e fontes Geist Sans/Mono incorporadas para funcionar sem serviço externo de fontes.

O ResMode não encerra jogos, não altera saves e não modifica os arquivos dos jogos. Detecção e restauração dependem dos eventos do Windows, do driver e do comportamento de cada aplicativo. Testes automatizados não substituem validação de jogos e múltiplos monitores em uma sessão real.

## Compilar a partir do código

No Windows PowerShell, na pasta deste projeto:

```powershell
./Build.ps1
./Test.ps1
./Package.ps1
```

`Build.ps1` usa o compilador .NET Framework do Windows, gera o ícone e incorpora as fontes e sua licença. Não baixa dependências, não instala e não abre o aplicativo. `Test.ps1` executa verificações de geometria, exclusões, classificação de caminhos, fontes, cores e ciclo do hover. `Package.ps1` gera o ZIP com os arquivos necessários, documentação e checksums, sem incluir testes ou dados locais.

Um checkout do código não contém executável. Nesse caso `Instalar.cmd` chama a compilação antes dos autotestes. Para recompilar deliberadamente uma cópia existente, use `Build.ps1` antes de instalar; a recompilação produz um executável unsigned e substitui qualquer assinatura anterior em `dist`.

O [workflow de CI](.github/workflows/build.yml) faz a mesma compilação e os testes em um runner Windows hospedado pelo GitHub. O processo é verificável a partir do código; não há promessa de binários idênticos byte a byte entre compilações do compilador legado.

## Privacidade

Configurações, exclusões, catálogo, histórico, tempo de jogo e posições de janelas ficam no PC, em `%LOCALAPPDATA%\ResMode`. As capas e conquistas da Steam são lidas do cache local.

Durante a descoberta da biblioteca, o app pode consultar automaticamente `store.steampowered.com/api/appdetails` com o ID público do app para distinguir jogos de outros programas. A Steam recebe também os dados normais da conexão. Não há conta ResMode, analytics, upload de logs ou envio de caminhos locais aos mantenedores. Leia a [política completa](PRIVACY.md).

## Code signing policy

Status, papéis da equipe, aprovação manual e origem dos binários estão em [CODE_SIGNING.md](CODE_SIGNING.md). A integração com SignPath permanece desativada até a aprovação do projeto e a configuração dos pré-requisitos; a preparação não garante aceitação.

## Licença e contribuições

Código, documentação e logo do projeto: [MIT](LICENSE). Geist Sans e Geist Mono: SIL OFL 1.1; consulte [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Capas de jogos não fazem parte da distribuição.

[Contribuir](CONTRIBUTING.md) · [Segurança](SECURITY.md) · [Preparação da solicitação SignPath](docs/SIGNPATH_APPLICATION.md)
