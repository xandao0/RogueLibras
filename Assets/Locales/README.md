# Locales — textos e sinais do RogueLibras

Todo texto que o jogador vê sai desta pasta. Nenhum texto fica escrito em
cena, prefab ou código: eles pedem o texto por **chave** (`menu.play`,
`settings.close`...) e o jogo mostra a língua ativa.

```
Locales/
  Resources/LocaleSettings.asset   quais línguas existem e quais estão em uso
  pt-BR/
    strings.json                   todos os textos em português (a referência)
  libras/
    glosses.json                   quais textos são sinalizados, e com quais sinais
    dictionary.json                o código FSW (SignWriting) de cada sinal
    signs/<categoria>/*.svg        os sinais desenhados (gerados do dictionary.json)
    SignWritingLibrary.asset       os sinais prontos para o jogo (gerado)
  translation-report.txt           o que falta traduzir (gerado)
```

## Dois tipos de língua

- **Língua escrita** (`pt-BR`; no futuro `en`, `es`...): um `strings.json`
  com o texto de cada chave. Chaves aninhadas viram pontos:
  `{ "menu": { "play": "Jogar" } }` é a chave `menu.play`.
- **Língua de sinais** (`libras`; no futuro `asl`, `lsm`...), escrita em
  SignWriting: um `glosses.json` dizendo, por chave, quais sinais mostrar, mais
  o dicionário e os SVGs desses sinais.

As duas são usadas juntas: os menus mostram o sinal ao lado da palavra, e as
cartas trocam palavra por sinal (ou sinal por palavra) aos poucos, turno a turno.

## Editar um texto

Abra `pt-BR/strings.json` e mude o valor. `{0}`, `{1}` são números que o
jogo coloca (ex.: `"Save {0}"`). Cartas, inimigos e efeitos ficam em `cards`,
`enemies` e `effects`, pelo nome do arquivo do asset (`cards.Fogo.name`).

Carta nova? Rode `RogueLibras > SignWriting > Buscar sinais novos` (ou
`node svg.js --offline`): o texto dela entra sozinho no `strings.json`, e
nenhum texto que já está lá muda.

## Adicionar, trocar ou tirar um sinal (Libras)

Os sinais vêm do `libras/dictionary.json` (código FSW de cada palavra).

- **Adicionar**: crie o sinal no [SignMaker](https://www.signbank.org/signmaker),
  copie o FSW e cole no `dictionary.json`, na categoria certa:
  `"Pao": "M518x529S..."`.
- **Trocar**: mude o FSW da palavra no `dictionary.json`. O SVG é refeito.
- **Tirar**: apague a palavra do `dictionary.json`. O SVG some, e o jogo
  mostra só o português.
- Também dá para colocar um SVG pronto direto em `libras/signs/<categoria>/`
  (o nome do arquivo é a palavra, sem acento: `Pao.svg`). O script nunca apaga
  um SVG colocado à mão.

Depois de mexer: `RogueLibras > SignWriting > Buscar sinais novos (SignPuddle)`
(ou `node svg.js` e `RogueLibras > SignWriting > Atualizar sinais`).

O SignPuddle (Dicionário Brasil) só dá **sugestões**: nada de lá entra no
jogo sozinho. Elas ficam em `revisar.html`, na pasta do svg.js; para usar uma:
`node svg.js --escolher Pao=3`.

## Sinalizar um botão ou menu

Hoje nenhum botão ou menu tem sinal - só português. Para sinalizar um, coloque
a chave dele em `libras/glosses.json` com o(s) sinal(is), separados por espaço
(cada um é uma entrada do `dictionary.json`; `Encerrar_Turno` é uma entrada
só, com dois sinais):

```json
"menu": { "play": "Jogar" }
```

O botão passa a mostrar o sinal ao lado da palavra assim que `Jogar` existir
no `dictionary.json`.

Nome e descrição das cartas **não** precisam estar no `glosses.json`: são
sinalizados palavra por palavra a partir do texto em português.

## Modo Português / Libras

O botão de idioma (ao lado do Som) troca os menus entre **Português** e
**Libras**. No modo Libras, todo texto que o `glosses.json` traduz por
completo (todos os sinais existem) aparece em sinais, sem as palavras; o que
não tem tradução completa continua em português. No modo Português, os menus
mostram o texto, com o sinal ao lado onde houver.

## O que falta traduzir

`RogueLibras > Localização > Relatório de tradução` escreve
`translation-report.txt` (e o svg.js escreve o mesmo em `missing-signs.txt`):

- **sinais que faltam**: palavras que o jogo mostra e não têm SVG;
- **textos traduzidos**: aparecem em sinais no modo Libras;
- **tradução incompleta**: a glosa existe mas falta sinal (diz qual);
- **sem tradução**: ficam em português, com as palavras do texto que já têm
  sinal e as que não têm - um começo para escrever a glosa;
- para cada outra língua escrita, as chaves ainda não traduzidas.

## Adicionar uma língua

**Escrita** (ex.: inglês): crie `en/strings.json` copiando o `pt-BR` e
traduzindo os valores (chaves que faltarem usam o português), e adicione-a em
`Text Locales` no `LocaleSettings`.

**De sinais** (ex.: ASL): crie `asl/` com `glosses.json`, `dictionary.json` e
um `SignWritingLibrary` (Create > RogueLibras > SignWriting Library), adicione-a
em `Sign Locales` no `LocaleSettings`, e rode o svg.js com
`--lingua asl --texto en` (o SignPuddle de ASL é outro dicionário; troque o
`sgn` no começo do svg.js).

Hoje o jogo usa `defaultText` e `defaultSign` do `LocaleSettings`; uma tela de
escolha de língua só precisa chamar `Loc.Use("en", "asl")`. Duas coisas ainda
assumem uma língua de sinais só: o atlas de sinais nas descrições (é um só,
o sprite asset padrão do TextMesh Pro) e a troca palavra a palavra nas cartas,
que pressupõe que o texto escrito é a língua das glosas (pt-BR para Libras).
