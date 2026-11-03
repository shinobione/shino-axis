# ADR-001 — Fondation AXIS : décision proposée en attente

> Archive P1 : le HOLD ci-dessous décrit la comparaison initiale, pas le produit actuel. Dès V0.1, AXIS a adopté .NET 10/WPF sans prétendre à une victoire de benchmark. Voir [architecture actuelle](../ARCHITECTURE.md) et [livraison V0.1](../V0.1-DELIVERY.md). Les résultats et incertitudes historiques restent conservés.

**Statut : PROPOSED / HOLD — aucun socle adopté.** 2 novembre 2026. Cette note ne valide ni migration ni remplacement de produit existant.

## Contexte et preuves

AXIS doit fédérer des sources existantes sans doubler leurs acquisitions, conserver les contrats Android HUB v2 et SmallTV, et rester extensible sous Windows. P0 distingue code local/distant et installation physique ; P1 conserve ces niveaux. Le mandat P1 remplace le candidat documentaire Node-RED par Macro Deck 3.0.0.

Base AXIS `5a32a5ce1d50f52e5b438d5fa9b9f162ea14a9de`, branche `codex/p1-foundation-lab`. A utilise SDK .NET 10.0.403 épinglé ; B le SDK source Macro Deck v3.0.0 au SHA `614dc0bbb04be21434176208fadff45179a6d5ec`. [Comparaison](../p1/P1-COMPARISON.md), [tests](../p1/P1-TEST-RESULTS.md), [benchmarks](../p1/P1-BENCHMARKS.md), [code et reproduction](../../lab/p1/README.md).

14 scénarios synthétiques passent sur chaque banc, dont les limites temporelles, réseau, rejeu, commandes et projection SmallTV. Ils valident largement **un adaptateur partagé**, pas deux moteurs natifs indépendants. Une session officielle SDK Macro Deck transporte effectivement la variable snapshot sans perdre sa date source. Ni UI complète ni ressources comparables n'ont été qualifiées.

## Options

**A — hôte minimal .NET 10/ASP.NET Core + shell Windows.** Force : maîtrise des frontières et faible surface du prototype, conservation explicite des dates/rôles/états ambigus. Faiblesse : ce code de laboratoire ne livre pas la supervision, le tray, l'installation, le transport sécurisé et la robustesse d'un produit complet. Affinité C# ne garantit pas la compatibilité binaire avec PERISCOPE .NET Framework. Coût futur de ces briques et ressources : inconnu.

**B — Macro Deck 3.0.0.** Force documentaire : SDK/protocole public, plugins hors processus, UI/tray Web/Tauri, nombreuses capacités déjà présentes ; première invocation SDK prouvée. Faiblesse observée : le démarrage complet n'est pas confiné par la configuration examinée (listeners publics explicites, services natifs et bootstrap). Un patch des listeners seul n'apporte pas la preuve d'absence d'I/O réelles. Coût d'adaptation des garanties SHINO et poids du produit complet non mesurés. Licences/notices et composants à conditions particulières à examiner avant redistribution.

**Alternative documentaire — Node-RED.** Pas de troisième prototype P1. Le modèle de flux resterait à comparer à la même autorité, aux mêmes protections durables et à un shell Windows ; aucun succès déduit de P0. Tauri/Rust ou Wails/Go restent d'autres hypothèses sans expérience exécutée ici.

## Proposition

**Ne sélectionner aucun des deux candidats à ce stade.** A est la piste de poursuite la plus directe pour maîtriser les contraintes du laboratoire, mais ce n'est pas une supériorité mesurée de fondation. Confiance faible sur le choix global, modérée sur la faisabilité de l'adaptateur testé. B reste à qualifier, pas éliminé définitivement. Les écarts de contrat/sécurité ne sont jamais compensés par une note de performance supposée.

La prochaine étape unique est un **plan de confinement statique du démarrage complet Macro Deck** : relier chaque I/O à une garde officielle ou une substitution minimale précise ; borner le coût de patch sans fork profond. L'Advisor a confirmé la nécessité de cette preuve ciblée. Critère : aucun chemin externe non contrôlé et un périmètre de modifications défendable avant tout lancement complet. Si ce confinement exige une refonte disproportionnée, suspendre B complet et proposer une évaluation suivante explicitement autorisée ; Node-RED serait alors l'alternative documentaire initiale à réexaminer, sans choix implicite.

## Conséquences et retour arrière

Conserver LINK, RELAY, HUB, CONTROL, PERISCOPE et leurs autorités. Ne pas retirer de VM ni modifier appareil/firmware. Le laboratoire ne devient pas un service installé. Tout benchmark futur inclut l'arborescence et la GUI ; « SDK contre hôte minimal headless » n'est pas une comparaison de produits. Les prérequis ouverts restent visibles, sans statut LIVE ni PASS de production.

Retour arrière actuel : arrêter les seuls processus enfants du laboratoire ; conserver les runs synthétiques pour analyse. Aucun déploiement n'a remplacé une installation. Les modifications AXIS restent sur la branche dédiée, sans push/merge/publication. Le signalement de création automatique du certificat de développement par le SDK est conservé ; aucune suppression globale n'est autorisée par cette ADR.
