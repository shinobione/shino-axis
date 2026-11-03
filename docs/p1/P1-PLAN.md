# P1 — Plan du laboratoire TEST

Mandat : mission P1 fournie le 2 novembre 2026. Prototypes jetables autorisés, aucune fondation définitive ni migration. Travail dans le checkout officiel, branche `codex/p1-foundation-lab`, base propre `5a32a5ce1d50f52e5b438d5fa9b9f162ea14a9de`. Les cinq rapports P0 sont committés dans cette base ; leurs conclusions sont conservées.

## Conception

A : ASP.NET Core/.NET 10, registre statique, producteurs TEST distincts, cache et journal bornés, interface Windows minimale. B : plugin du SDK Macro Deck 3.0.0, mêmes données et mêmes clients ; aucun fork profond. Node-RED reste documentaire. Un moteur indépendant reconstruit pour B ne prouverait pas la valeur de Macro Deck : les résultats doivent identifier la frontière réellement exercée.

Les contrats partagés sont des fixtures et des assertions, pas une substitution des schémas SHINO. Les dates sont fixes et l'horloge injectée. Les domaines gardent leurs dates source, rôles et watermarks. SMALLTV_SIM reçoit une projection numérique distincte. Aucune acquisition réelle. Le troisième module doit utiliser le point d'extension du candidat sans modifier les deux producteurs.

## Exécution prévue

1. [x] Vérifier Git/P0 et outils ; lecture intégrale des neuf documents obligatoires.
2. [x] Sources B/SHA, SDK local et licences inspectés. Incident : premier lancement SDK signalant certificat dev automatique, consigné ; bootstrap ultérieur durci.
3. [x] Corpus et 14 scénarios HTTP communs, avec schémas existants chargés à l'identique ; rapports JSON expurgés.
4. [x] Hôte A compilé/testé ; interface compilée, rendu/tray et produit complet non qualifiés.
5. [x] Plugin B/SDK officiel compilé/testé, vraie session WebSocket synthétique ; produit complet suspendu au confinement. L'adaptateur commun n'est pas présenté comme moteur natif B.
6. [x] Troisième module de l'adaptateur ajouté ; coût constaté dans la comparaison, extension native B indépendante non qualifiée.
7. [ ] Si les barrières critiques passent : trois répétitions A/B alternées, 60 s chauffe + 600 s repos + 600 s charge ; toute l'arborescence et GUI incluses. Sinon NON MESURÉ et aucun benchmark long.
8. [x] Rapports comparaison, résultats, benchmarks NON MESURÉS, ADR PROPOSED/HOLD ; aucun push/merge/publication.

## État final

Étape 7 non exécutée : barrières du comparatif complet ouvertes. Prototype, corpus et preuves partiels livrés ; **mission P1 non close par un choix de fondation**. Une consultation Advisor ciblée et sans modification confirme le HOLD et le besoin d'un plan de confinement B. Les logiciels SHINO n'ont pas été lancés/modifiés. La revue de fin est celle de l'auteur pour le code, pas une revue indépendante exhaustive.

## Prérequis et arrêts

Runtime .NET 10.0.12 présent, SDK absent au relevé initial. Sources B : tag stable vérifié par `git ls-remote` ; compilation et exécution non encore qualifiées. Les téléchargements et caches restent dans `lab/p1/.cache/`, ignoré. Refuser tout démarrage B tant que ses options ne garantissent pas loopback, profil local et absence d'autostart/intégrations réelles. Une nécessité de modification système exige un accord séparé.

## Points de vérification

Ports loopback uniquement ; pas de HTTP vers un produit existant ; protocole TEST explicitement séparé des interfaces de production ; état borné et conservation des watermarks ; pas de donnée privée dans les fichiers versionnables ; ACK distinct du résultat ; aucun succès d'un test du harness présenté comme test d'un candidat. Les cas non exercés restent NON TESTÉ, les performances sans protocole complet NON MESURÉ.
