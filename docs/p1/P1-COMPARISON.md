# P1 — Comparaison du laboratoire Foundation

**2 novembre 2026 — résultat partiel, décision HOLD.** Les deux prototypes compilent et passent 14 scénarios communs. Cela ne départage pas deux plateformes complètes : B exerce le banc officiel du SDK Macro Deck, tandis que le contrat SHINO est implémenté par le même adaptateur de laboratoire dans A et B. Aucun choix définitif ni migration n'est validé.

## Sources et périmètre

- AXIS officiel : `https://github.com/shinobione/shino-axis`, checkout officiel, base `main` propre `5a32a5ce1d50f52e5b438d5fa9b9f162ea14a9de`. Cinq P0 présents dans HEAD, aucun changement P0 indexé/non suivi. Branche créée : `codex/p1-foundation-lab`. Aucun fetch du main AXIS : l'identité distante actuelle n'est pas attestée par la seule URL du remote. Ancienne copie SHINO-AXIS non utilisée.
- Lecture intégrale : README, AGENTS, ARCHITECTURE, ROADMAP et les cinq P0. Pas de nouvel audit global des systèmes SHINO. Lectures techniques supplémentaires limitées aux deux schémas snapshot locaux et au parseur SmallTV déjà référencés par P0 ; aucune exécution de leurs logiciels.
- A : SDK .NET **10.0.403**, `rollForward=disable`, archive officielle Windows x64 contrôlée SHA-512 dans le bootstrap ; ASP.NET Core et WinForms. Aucun SDK installé au relevé initial, runtimes globaux dont .NET/WindowsDesktop 10.0.12 présents ; SDK ajouté uniquement au cache AXIS.
- B : [release v3.0.0 stable](https://github.com/Macro-Deck-App/Macro-Deck/releases/tag/v3.0.0), tag résolu par Git à `614dc0bbb04be21434176208fadff45179a6d5ec`, clone local au même SHA. `global.json` upstream demande 10.0.401 avec roll-forward ; le banc compile avec 10.0.403 épinglé. Aucun fichier source tiers suivi n'est modifié. Le README du tag contient encore un avertissement de développement ; la métadonnée de release indique stable. Ces deux faits sont conservés.
- Schémas locaux effectivement chargés : RELAY SHA-256 `8bed0e07943cd23232a7265dcbc6b7ade0bbf708f198c57b75e3f465f38bdd6b`, Android `03fe036cd9d47ba9e28d296bc19a06f26a9b72beed884c33ebec6f24d5deec83`. Ils correspondent aux empreintes P0. Ce sont des sources locales, pas des contrats négociés avec un appareil installé.

## Résultats comparables et limites

| Critère | A — hôte minimal | B — SDK Macro Deck |
|---|---|---|
| Contrats de données | 14 scénarios du banc passent | Les mêmes 14 passent sur l'adaptateur commun ; une lecture réelle de variable traverse le protocole WebSocket SDK |
| Deux producteurs | Fonctions LINK_SIM/PERISCOPE_SIM indépendantes dans le corpus Python ; injections HTTP | Identiques ; pas deux collecteurs Macro Deck autonomes |
| Deux clients | ANDROID_SIM lit le snapshot ; SMALLTV_SIM reçoit la projection numérique | Identiques ; aucune application Android/SmallTV réelle |
| Registre, état, événements, commandes | Registre connu, état versionné borné 64 Kio, journal 128 événements, 128 commandes sans éviction des clés | Ces mécanismes appartiennent à l'adaptateur commun, pas aux moteurs natifs de Macro Deck |
| Isolation des erreurs | Arrêt logique d'un producteur vérifié sans perte du quota ; crash d'un processus adaptateur NON TESTÉ | Session SDK exercée dans le processus B avec host synthétique ; isolation entre processus de plugins NON TESTÉE |
| Extensibilité | Troisième module ClockModule, un fichier de 7 lignes + une ligne d'enregistrement | Même point d'extension de l'adaptateur, même coût ; ajout d'un plugin natif Macro Deck indépendant NON TESTÉ |
| UI / tray | Fenêtre WinForms compilée, modules et snapshot observables ; rendu et tray NON QUALIFIÉS, pas de tray implémenté | Angular/Tauri/tray décrits par les sources ; produit complet et UI NON EXÉCUTÉS |
| CONTROL Node.js | Frontière future de processus/HTTP plausible ; pas de CONTROL lancé ou modifié | Protocole plugin JSON/HTTP/WebSocket documenté pour d'autres langages ; aucun adaptateur CONTROL qualifié |
| PERISCOPE C# | Adaptation hors processus plausible ; le .NET Framework existant n'est pas une DLL net10 automatiquement compatible | Même remarque ; SDK C# n'élimine pas l'adaptation nécessaire |
| LINK/RELAY | Projection synthétique, aucune nouvelle collecte réelle | Idem |
| Installation/démarrage/veille/reprise | Build local et reprise de l'état TEST qualifiés ; installation et veille NON TESTÉES | Reprise du banc SDK qualifiée ; installation/veille/produit complet NON TESTÉS |
| CPU/RAM/latence | NON MESURÉ | NON MESURÉ ; le banc SDK ne représente pas le coût du produit complet |
| Maintenance | Petit périmètre propre, mais supervision, shell et garanties de transport restent à produire | Nombreuses fonctions existantes ; coût d'adaptation SHINO et de confinement restant inconnu |

Le coût de ClockModule décrit l'extension **de l'adaptateur de laboratoire seulement**. L'interface générique `ITestModule` était ajoutée pour ce test ; elle n'exige ensuite aucune modification de la logique LINK_SIM/PERISCOPE_SIM. Aucun changement du noyau Macro Deck. Ce résultat ne prouve pas l'installation, la découverte, l'isolation ni l'évolution de plugins tiers sur plusieurs années. Temps de développement et difficulté de débogage : pas de chronométrage fiable, donc NON MESURÉS ; erreurs concrètes consignées dans le rapport de tests.

## Pourquoi le produit Macro Deck complet n'est pas lancé

Au SHA stable : `HostListenerPlan.Apply` ouvre les listeners publics HTTP/HTTPS avec `ListenAnyIP`. `PublicPortSelector` ne propose pas de port désactivé ; le repli est le port public par défaut. `PublicTlsSelector` peut remplacer HTTP par HTTPS, sans supprimer toute écoute publique. Le listener loopback supplémentaire ne confine pas les listeners publics. Une variable ASPNETCORE_URLS ne remplace pas ces bindings explicites.

`Program` effectue un bootstrap TLS avant la construction de l'hôte. `Startup` inscrit notamment les services ADB, USB natif, intégrations et rafraîchissement autostart. **Inscription n'est pas preuve d'effet certain** : ADB a une garde d'activation ; l'autostart requiert notamment un shell valide et une inscription existante à rafraîchir. Aucun effet réel de Macro Deck n'a été observé puisque le produit n'a pas été lancé. L'absence simultanée d'I/O réelles et d'effets globaux n'est cependant pas démontrée par ces réglages.

Une consultation unique Advisor en lecture seule a confirmé que changer seulement les deux listeners ne suffit pas à prouver le confinement du démarrage complet. Aucun mode officiel satisfaisant toutes les contraintes n'a été trouvé dans ces chemins ciblés ; ce n'est pas une preuve exhaustive d'inexistence. Construire un hôte Macro Deck fortement amputé fausserait aussi la mesure. Le banc officiel SDK permet une preuve utile sans prétendre résoudre cette limite.

## Dépendances, licences et redistribution

Versions résolues et métadonnées inspectées : [inventaire JSON](../../lab/p1/shared/dependencies.json), fichiers `packages.lock.json`, manifeste central et notices B au SHA épinglé. Les caches et binaires ne sont pas versionnés. Les packages d'exécution du banc sont distincts de ceux du produit complet.

Macro Deck racine : Apache-2.0 ; son nom/logo/marque sont exclus de cette licence selon NOTICE. Le compagnon natif mobile est optionnel et payant selon le README du tag ; le client Web est proposé sans ce compagnon. Aucun compagnon n'est nécessaire au banc ; cela ne rend pas les clients SHINO automatiquement compatibles.

Attention aux licences binaires : JsonSchema.Net 9.4.0, JsonPointer.Net 7.0.2 et Json.More.Net 3.0.1 déclarent une **OSMF EULA**, pas simplement une licence MIT de package. Le texte prévoit une contribution pour certains usages générateurs de revenus à partir de 10 000 USD annuels et distingue source MIT, binaires fournis et auto-compilation. Humanizer.Core déclare MIT ; Serilog Apache-2.0. L'utilisation présente est un laboratoire synthétique sans activité commerciale décrite ; aucune redistribution ni validation juridique générale n'est approuvée. Ne pas réutiliser le binaire en produit sans examiner les conditions applicables.

Le NOTICE de B mentionne ImageSharp sous Six Labors Split License, exemption open source revendiquée par Macro Deck ; cette revendication ne se transfère pas automatiquement à un produit AXIS dont la licence n'est pas décidée. Les notices incluent également des composants sous d'autres licences. Les lockfiles npm/Rust et leurs empreintes sont relevés sans installation UI. Aucun inventaire partiel ne vaut approbation des dépendances transitives du produit complet.

## Recommandation et prochaine action unique

**Aucune fondation retenue.** A reste une piste simple à poursuivre, avec confiance faible à modérée sur sa faisabilité contractuelle, faible sur son avantage global. B n'est pas rejeté comme architecture ; sa qualification complète reste suspendue au confinement. Node-RED demeure une alternative documentaire sans troisième prototype.

**Prochaine action : établir un plan statique de confinement du démarrage complet Macro Deck 3.0.0.** Critère de réussite : chaque I/O externe est reliée à une garde officielle ou à une substitution minimale précise, sans I/O réelle non contrôlée, et le nombre/coût des patches est borné. Cela permettra de décider si l'essai complet respecte encore l'interdiction d'un fork profond. Aucune migration n'en découle automatiquement.

## Opérations non effectuées et incident

Aucun lancement/modification LINK, RELAY, HUB, PERISCOPE, CONTROL ; aucun vrai Codex app-server, secret ou historique privé lu ; aucun accès LAN, Android, SmallTV, Freebox, VM, ADB, SSH, flash, OTA ou opération matérielle ; aucun service/registre/pare-feu modifié intentionnellement ; pas d'installation globale de logiciel, push, merge ou publication.

**Effet de bord à conserver :** le premier lancement du SDK a signalé la création automatique d'un certificat de développement ASP.NET Core. Cette génération n'avait pas été désactivée avant cet appel ; elle l'est dans le bootstrap livré. Aucune commande de confiance ni suppression n'a été exécutée, aucun magasin de certificats inspecté. Ne pas présenter cette session comme exempte de tout effet global potentiel.
