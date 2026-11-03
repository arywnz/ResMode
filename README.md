# ResMode

> **Aviso antes de tudo:** o ResMode foi feito para ser usado com upscaling **FSR 4 ou DLSS**. Resolução maior + upscaler decente é o dream dos videojogos e games. Se o jogo tem FSR 3, o driver da AMD consegue trocar por FSR 4 na hora, então está tudo bem. Agora, de FSR 2 para baixo a imagem fica uma merda, toda farinhenta, e nem o ResMode te salva disso. Aceite o destino ou troque de jogo e LEVE em consideração que resoluções mais altas o uso de VRAM é maior.

**Você gastou uma fortuna na placa de vídeo pra ficar refém de um monitor 1080p de betinha.**

O ResMode nasceu de um problema extremamente específico: querer jogar em uma resolução maior sem precisar transformar o desktop inteiro num experimento científico toda vez que abre um jogo.

Ativou o DLDSR, foi jogar em 1440p ou 4K, terminou a jogatina e teve que voltar nas configurações do Windows para desfazer a brincadeira?

Pois é,Eu também achei uma porra fazer isso toda hora.

## Mas que desgraça esse programa faz?

É simples:

1. Você deixa o Windows na resolução normal, como uma pessoa civilizada.
2. Abre um jogo que configurou no ResMode.
3. O programa detecta o jogo e muda a resolução automaticamente.
4. Você joga com a resolução maior que escolheu.
5. Fecha o jogo e o ResMode devolve o desktop ao normal.

**Pronto. Era só isso que eu queria.**

Infelizmente, o Windows achou que seria engraçado complicar as coisas, então agora o programa também precisa cuidar das janelas, dos outros monitores e de umas situações que eu preferia nunca ter descoberto.

**Não testei com mais de dois monitores.** Então, já sabe, né, paezão? Se você tem três ou mais, faz o L (de **levar o feedback lá nas issues**, obviamente) e conta se essa budega funcionou ou se o Windows resolveu cometer um crime.

## Pra quem é essa geringonça?

- **Usuários de DLDSR/DSR da NVIDIA:** para quem tem um monitor humilde, mas quer renderizar jogos em resoluções maiores.
- **Usuários de VSR da AMD:** porque a NVIDIA não é dona da brincadeira.
- **Quem usa dois ou mais monitores:** e já viu o Windows reorganizar tudo como se tivesse jogado os monitores dentro de um liquidificador.
- **Quem tem preguiça de configurar tudo manualmente:** provavelmente o público-alvo mais importante.
  
- **E a Intel Arc?**

Não tem suporte oficial, meu consagrado, quem é que compra essa porraKKKKKKKKKKKKKKKK

Piadocas a parte, não foi testado em placas Intel Arc(pasmem, nao tenho acesso a uma) Se você é um dos 7 proprietários de uma dessas, pode até funcionar, mas não prometo absolutamente nada. Se testar, abre uma issue contando o resultado, a ciência e o pcsismo agradece.

Se você joga sempre na resolução nativa e está feliz assim, talvez não precise disso. Aproveite sua paz.

## O que tem dentro?

- Biblioteca de jogos com capas, busca e organização por launcher.
- Detecção de jogos da Steam, Epic, GOG, Ubisoft, EA, Battle.net, Riot e Rockstar.
- Adição manual de jogos que o programa não encontrar.
- Troca automática de resolução ao abrir e fechar os jogos selecionados.
- Suporte a múltiplos monitores, com opção de centralizar o segundo monitor.
- Tentativa de restaurar as posições das janelas depois da jogatina.
- Recuperação da resolução original caso o aplicativo seja encerrado inesperadamente.
- Histórico de atividade para descobrir qual foi a última gracinha que o Windows aprontou.
- Atualizações pelo próprio aplicativo.

Tudo isso sem precisar instalar um launcher para abrir outro launcher que finalmente abre o jogo.

## Como instalar

**[Baixar o ResMode para Windows (.msi)](https://github.com/arywnz/ResMode/releases/download/v0.2.2-alpha.24/ResMode-0.2.2-alpha.24-win-x64.msi)**

Funciona no Windows 10 e 11 de 64 bits.

Instale, abra o programa, escolha seu monitor e a resolução desejada. Depois, marque os jogos que quer acompanhar.

**Atenção, criatura:** você precisa ativar o DLDSR, DSR, VSR ou outra resolução compatível no driver da placa de vídeo antes. O ResMode não tira resolução do além.

O instalador ainda não possui assinatura digital. Portanto, o Windows pode reclamar ou bloquear a instalação. O código está disponível neste repositório para inspeção.

## Algumas verdades inconvenientes

**Isso aumenta FPS?**

Não, meu consagrado. Renderizar mais pixels normalmente exige *mais* da placa de vídeo. O objetivo aqui é facilitar o uso de resoluções maiores, não fazer sua RX 580 xing xong surrada do aliexpress virar uma RTX 5090.

**Funciona com qualquer jogo?**

A ideia é funcionar com a maior quantidade possível, mas alguns jogos têm opiniões muito fortes sobre como o monitor deve funcionar. O comportamento pode variar conforme o jogo, o modo de exibição e o driver.

**O Alt+Tab funciona?**

O ResMode tenta manter a resolução de jogo durante a sessão, inclusive ao alternar janelas. Isso significa que o próprio desktop também pode permanecer na resolução maior enquanto você joga.

**Tem que pagar?**

Não. É gratuito e de código aberto.

**Vai mexer nos arquivos dos meus jogos?**

Não. O ResMode não modifica jogos, saves nem injeta código nos processos.

## Privacidade, porque ninguém merece mais um programa bisbilhoteiro

Sem conta. Sem anúncios. Sem telemetria enviando sua vida para algum servidor misterioso.

As configurações, a biblioteca e os registros ficam no seu computador, com proteção de dados locais pelo Windows DPAPI.

O aplicativo pode consultar a Steam para identificar jogos e o GitHub para verificar atualizações. Os logs só são exportados quando você solicita.

Mais detalhes em [PRIVACY.md](docs/PRIVACY.md).

## Ainda estamos na fase das experiências

O ResMode está em **alpha**.

Isso significa que tem funcionalidades prontas, testes automatizados e também a possibilidade de algum jogo decidir que não quer colaborar.

Encontrou um bug? [Abra uma issue](https://github.com/arywnz/ResMode/issues). Explique o que aconteceu e, se possível, envie o log gerado pelo aplicativo, depois de conferir se não tem informações pessoais.

Se quiser compilar, veja [CONTRIBUTING.md](docs/CONTRIBUTING.md).

Licença [MIT](LICENSE). As fontes Geist possuem licença própria, descrita em [THIRD_PARTY_NOTICES.md](docs/THIRD_PARTY_NOTICES.md).

---

