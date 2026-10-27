---
name: translate-moneyrecord
description: Add or update a MoneyRecord translation (app strings, widget, Play listing, What's new). Use when asked to add a language, translate new strings, or check translation terminology.
---

Languages are the rows of `AppLanguage.All` (`MoneyRecord.Core/Models/AppLanguage.cs`): en, es, pt (pt-BR), fr, it.
The picker, device detection, culture fallback, Play changelog locale and plural rule all read that list.

## New strings in a feature PR
Add the English key to `AppResources.resx`, then the same key (same `{n}` placeholders) to every `AppResources.xx.resx`.
`ResourceParityTests` fails per language until all exist. Use the glossary below; keep tab labels, chips and buttons short
(pt/fr/it run 20-30% longer than English). Informal address: es tú, pt você, fr tu, it tu.

## Adding a language
1. One row in `AppLanguage.All` (`Code`, default culture = Play store locale, `ZeroIsSingular` for French-like plurals).
2. `MoneyRecord.Core/Resources/Strings/AppResources.xx.resx`: all keys, no `<comment>`s, header copied from `AppResources.es.resx`.
3. `MoneyRecord/Platforms/Android/Resources/values-xx/strings.xml` (the 7 widget strings; escape `'` as `\'`).
4. `fastlane/metadata/android/<locale>/`: `title.txt` (30), `short_description.txt` (80), `full_description.txt` (4000),
   `changelogs/default.txt` (500), `changelogs/history.txt` (same `## x.y.z` headings as en-US).
5. A `MauiAsset` line for its `history.txt` in `MoneyRecord/MoneyRecord.csproj` (`Changelog/<locale>.txt`).
6. Receipt keywords in `ReceiptReader` (total, subtotal, change, tax, month names) plus a test.
7. `dotnet test` from PowerShell (parity and `LanguageCoverageTests` list anything missing), then switch the language
   on the emulator (`/run-moneyrecord`) and look for truncated labels.

## Glossary
| English | pt-BR | fr | it |
|---|---|---|---|
| Account | Conta | compte | conto |
| Transfer | Transferência | virement (button Virer) | trasferimento (Trasferisci) |
| Transaction | Transação | transaction | movimento |
| Income / Expense | Receita / Despesa | revenu / dépense | entrata / spesa |
| Pocket (set aside / take out) | Cofrinho (Guardar / Retirar) | enveloppe | salvadanaio (Accantona / Preleva) |
| Budget | Orçamento | budget | budget |
| Payee | Favorecido | bénéficiaire | beneficiario |
| Split | Dividir / Pagamento dividido | répartir / paiement réparti | dividi / pagamento diviso |
| Debt | Dívida | dette | debito |
| Trip | Viagem | voyage | viaggio |
| Party (shared pot) | Evento (Caixinha) | événement (cagnotte) | evento (cassa comune) |
| Savings challenge | Desafio de economia | défi d'épargne | sfida di risparmio |
| Goal | Meta | objectif | obiettivo |
| Recurring | Recorrente | récurrente | ricorrente |
| Home / More | Início / Mais | Accueil / Plus | Home / Altro |
| Reports | Relatórios | Rapports | Report |
| Trends / Cash flow / Net worth | Tendências / Fluxo de caixa / Patrimônio | Tendances / Flux de trésorerie / Patrimoine net | Andamento / Flusso di cassa / Patrimonio netto |
| Settings | Configurações | Réglages | (see resx) |
| Backup | (see resx) | sauvegarde | (see resx) |
