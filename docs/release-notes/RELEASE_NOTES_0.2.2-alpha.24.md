# ResMode 0.2.2-alpha.24

**Ícones da barra de tarefas sem reiniciar o Explorer.** Depois de um jogo, o ResMode corrigia os ícones borrados matando e reiniciando o `explorer.exe`. O Windows registra isso como queda do shell ("o shell parou repentinamente"), a bandeja do sistema é esvaziada e programas como o Xbox Game Bar chegaram a cair. Agora o app avisa o Windows de que os ícones e as métricas de janela mudaram, e ele redesenha a barra de tarefas no lugar. Testado numa troca real de resolução: os ícones ficaram borrados depois da troca e voltaram nítidos depois do aviso, sem reiniciar nada.

A opção "Corrigir ícones borrados" agora vem ligada por padrão em instalações novas, já que deixou de ser invasiva. Quem já tinha a opção mantém a escolha que fez.

MSI para Windows x64, sem assinatura digital. Feche o ResMode antes de atualizar, quando nenhum jogo estiver aberto. As configurações são preservadas.
