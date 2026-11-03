# AXIS V1.0 — Architecture implémentée

Un processus Windows **.NET 10 / WPF**, avec WinForms uniquement pour le tray. Le runtime ASP.NET Core héberge le récepteur PROJECTS interne. Aucun Node.js, service Windows, VM ou Macro Deck requis. P0/P1 restent historiques ; le choix WPF V0.1 n'est pas une victoire mesurée aux benchmarks.

## Frontières et sources

| Source | Responsabilité |
| --- | --- |
| `src/Axis.Core/Runtime.cs` | `IAxisModule`, registre compilé, activation/arrêt sérialisés, snapshots datés, événements bornés à 200, préférences. |
| `src/Axis.Core/Modules` | PC, Network, Media, CODEX, PROJECTS et leur cycle de vie commun. |
| `src/Axis.Core/Missions` | Lecture Codex locale bornée, projection des tours, notifications. |
| `src/Axis.Core/Projects` | Stockage compatible CONTROL, import/fusion, attribution, statuts/reprises, deltas, compatibilité catchup, synchronisation GitHub. |
| `src/Axis.Core/Presentation` | Règles de présentation indépendantes de WPF. |
| `src/Axis.App` | Composition des cinq modules, cockpit, pages, paramètres, tray, animations. |
| `src/Axis.App/Collectors/WindowsMediaSource.cs` | Adaptateur SMTC de `IMediaSource`. |
| `tests/Axis.Core.Tests` | Suite exécutable synthétique et HTTP loopback. |
| `tests/Axis.App.VisualChecks` | Recette graphique WPF synthétique explicite. |
| `lab/p1` | Laboratoire historique, sans dépendance produit vers lui. |

Modules enregistrés dans `MainWindow`, sans découverte arbitraire de DLL. Boucle séquentielle hors thread UI, sans empilement de ticks ; des opérations lentes peuvent retarder les cadences nominales. Tick UI d'une seconde pour durées/péremption. Désactiver un module arrête ses acquisitions et invalide son snapshot.

## PC, réseau, média

PC : différences `GetSystemTimes`, RAM physique `GlobalMemoryStatusEx`, identité CPU par registre. GPU NVIDIA 0 via `nvidia-smi`, timeout 1,5 s ; GPU AMD/Intel et température CPU absents. Premier CPU indisponible, collecte nominale 2 s. Compteurs incohérents ou interruption de plus de 10 s : réamorçage, sans pic inventé. Suspend/reprise et réactivation demandent aussi un réamorçage. Une erreur devient une observation indisponible, avec nouvelle tentative au cycle suivant.

Network : compteurs d'interface et horloge monotone, priorité à une interface active avec passerelle IPv4. Premier échantillon, changement d'interface et remise à zéro invalident le débit. Mo décimaux, volumes sur les intervalles observés ; aucun trafic de test externe.

Media : sélection SMTC stable (session précédente en lecture, session Windows en lecture, autre lecture ; puis pause précédente/courante). Sessions arrêtées/fermées exclues. Métadonnées par notification et relecture de secours toutes les 5 s, publication cohérente titre/pochette, pochette limitée à 4 Mio. WinRT borné à 2,5 s et désabonnement à l'arrêt. Sans PlaybackRate, position brute conservée ; extrapolation bornée à une ancre de 30 s, seulement en lecture avec vitesse et durée connues. Timeline limitée à sept jours. Champs absents effacés, lecture seule, aucune commande de lecture.

V0.9 : `PcSnapshot` et `MediaSnapshot` conservent les valeurs numériques nullable, unités explicites (`GiB`, pourcentage, °C, secondes), date de capture et fraîcheur maximale de 6 s. MEDIA garde aussi l'identité de session (stable pendant sa présence dans le gestionnaire) et la date de timeline Windows ; aucune identité inter-redémarrages n'est promise par SMTC. Ces objets sont en mémoire, sans sérialisation/protocole réseau ajouté. Les chaînes françaises restent la projection WPF compatible. Valeurs PC/MEDIA et pochette retirées de l'écran après péremption ; l'art d'une observation suivante n'est jamais attaché au titre précédent.

## CODEX : quotas et missions

`CodexQuotaSource` détecte le CLI par `SHINO_CODEX_EXE`, PATH ou emplacements usuels. Processus `app-server` stdio éphémère : `initialize`, `initialized`, `account/rateLimits/read`. Une requête au plus, timeout 20 s, annulation et nettoyage de l'arbre de processus. Aucun accès direct aux tokens ni corps RPC journalisé ; aucune conversation créée/reprise.

`QuotaParser` identifie les fenêtres par durée ; restant = 100 − utilisé. Catégories modernes prioritaires sur leur représentation historique. Crédits/reset seulement si fournis. V0.8 : 30 s par défaut, 30/60/120/300 s persistants, demande manuelle et reprise. Backoff par défaut 60/120/240/300 s ; requête antérieure annulée et réponse écartée à la reprise. Cache disque non déclaré frais avant réussite ; fraîcheur maximale `max(90 s, 2 × intervalle)`.

`quota-history.json` garde 1000 points maximum. Valeurs/reset/crédits identiques : continuité toutes les cinq minutes, sans écriture à chaque interrogation. Erreurs répétées dédupliquées, ruptures conservées ; pas d'interpolation trompeuse des lacunes ni déduction de reset.

`MissionObserver` pilote une seule lecture locale en vol, nominalement toutes les 5 s, indépendante du RPC. Source JSONL non documentée et sensible aux versions : métadonnées de cycle de vie seulement, sans persistance de prompts, sorties ou conversations. Rapport périmé après 25 s ; activité expirant après 90 s ou incohérence de date. Workspace/modèle/effort affichés si observés. Sous-agents séparés des racines ; silence/fin de tour ne prouvent ni fin de conversation Desktop ni fin de projet.

WPF conserve ses cartes et les jauges OBSIDIAN CIRCLE ; anneau actif seulement sur preuve récente, arrêté aux états terminaux/incertains ou au masquage. Six événements récents affichés, vingt observations dans la projection globale. Notifications de fins confirmées opt-in, sans rejeu au démarrage/reprise.

## PROJECTS et extension Chrome

`ProjectStore` conserve `projects.json`, compatible avec les identités/champs CONTROL. Mutations sérialisées sur un clone, écriture temporaire/remplacement avec `.bak` avant publication en mémoire. Stockage illisible protégé. Import prévisualisé puis fusion à trois voies avec baseline privée, conservation des modifications/affectations/suppressions locales et conflits visibles. Aucun HQ ou transcription complète importé par ce parcours.

`ControlEngine`, `ProjectResolver`, `ChatDelta` et les règles de reprise dérivent les états depuis les preuves et leur provenance. Choix manuels/archives prioritaires ; ambiguïté et preuves anciennes ne deviennent pas artificiellement ACTIVE. Fingerprint inchangé et relations cohérentes = aucune dérivation/réécriture. Réparation d'affectation : les preuves suivent la source.

Kestrel écoute **IPAddress.Loopback**, par défaut `127.0.0.1:4187`, et s'arrête avec PROJECTS. CONTROL CORE conserve 4177, sans arrêt/transfert automatique. Routes :

- `GET /api/health`
- `POST /api/ingest/chatgpt-delta`
- `POST /api/chatgpt/catchup-plan`
- `POST /api/chatgpt/catchup-report`

Gardes adresse distante/Host/Origin/Fetch, origine d'extension précise, JSON limité à 1 Mio, lecture 10 s ; Bearer optionnel comparé à temps constant. Réception privée désactivée par défaut. ID d'extension via options PROJECTS ou `AXIS_CONTROL_EXTENSION_ID`, token éventuel dans `SHINO_CONTROL_TOKEN`. `--projects-port <port>` isole les essais sans exposer le LAN.

L'extension Chrome installée est **externe au dépôt AXIS**. Sa collecte structurée utilise la branche active de l'API ChatGPT authentifiée, puis envoie un delta borné. Son code n'est ni distribué ni modifié ici. [Recette réelle et limites](CHROME-COLLECTION-VALIDATION.md). Les endpoints catchup existants conservent plans bornés/cooldowns/reports ; aucun rattrapage historique global n'est un objectif actif.

`ProjectGithub` lit l'API GitHub : ETags/curseurs, commits récents, PR/workflows et signaux de fichiers. Déduplication, réponse rejetée après réaffectation. Requêtes bornées à 20 s/4 Mio ; listes bornées, sans pagination exhaustive. Accès public ou `GITHUB_TOKEN`/`GH_TOKEN` dans l'environnement. Demande manuelle ou automatique opt-in toutes les cinq minutes. Aucun commit/push/merge.

## Persistance, distribution et intégrations

Profil `%LOCALAPPDATA%\SHINO\AXIS`, remplaçable par `--data-dir <dossier>` : `settings.json`, `quota-history.json`, `projects.json` avec preuves/curseurs/baseline et sauvegarde. Missions en mémoire. Profil privé hors Git ; fixtures synthétiques dans les tests. Une garde mutex/event Windows par utilisateur et profil exclut les doublons et restaure la fenêtre existante. Les profils isolés restent séparés pour les tests.

SDK 10.0.403 épinglé, Core `net10.0`, App `net10.0-windows10.0.19041.0`. Package Windows x64 autonome, dossier complet sans trimming/single-file. [Build et packaging](DEVELOPMENT.md).

V0.7 a importé les données CONTROL réelles ; la recette Chrome ultérieure est rapportée séparément. V0.8 : 71 tests, rendu/animations WPF et package autonome dans le rapport historique ; cadences **synthétiques**, sans nouvelle qualification du compte Codex réel ni veille physique. Aucun résultat ne certifie une parité exhaustive.

LINK, PERISCOPE et CONTROL restent partiellement portés. RELAY/HUB, Android TV, SmallTV et BRAIN sont futurs : aucun transport produit vers les appareils ici, aucun double producteur LINK/RELAY. Contrats HUB v2, TTL, identités et états inconnus restent des contraintes. Chaque bascule service/appareil exige preuves, retour arrière et autorisation propre. Fermer V1.0 puis relancer le package V0.9 préservé est le retour applicatif documenté, sans prétendre réparer un appareil.

## Consolidation V1.0

`CockpitStatus` distingue cycle AXIS et observation source : la date quota vient du dernier snapshot, pas du heartbeat runtime. Inconnu/périmé et absence de session restent distincts. Accueil : quatre missions maximum, expiration terminale 60 s conservée, alertes réelles, mesures/média/quotas puis sources et liens discrets.

Le runtime garde son verrou séquentiel ; après exception, réessai à 5/10/20/40/60 s (maximum), avec nettoyage avant redémarrage. Désactiver annule les réessais. La fermeture finale pose une barrière terminale avant arrêt, refuse les activations retardées, gèle l’interface et attend la dernière sauvegarde. Une fermeture avant Loaded ne réécrit aucun profil non chargé. Les acquisitions CODEX et PROJECTS ne changent pas.

Settings version 1 additive : fermeture tray optionnelle (false), animations (true), dernière page optionnelle (false), autostart (false). Champs inconnus préservés ; sauvegardes sérialisées et inchangées dédupliquées. La fermeture attend leur file. `StartWithWindows` enregistre une intention ; l’UI lit aussi la présence réelle de l’entrée dédiée et signale un emplacement différent. Aucun registre écrit au démarrage ni dans un profil isolé.

`WindowsIntegration` possède uniquement `HKCU/Software/Microsoft/Windows/CurrentVersion/Run` valeur `SHINO.AXIS`, uniquement sur action UI explicite. Le mutex ne couvre pas les anciens packages V0.9 : les quitter avant bascule. Tray : afficher, masquer, Accueil, Paramètres, quitter. Masquer stoppe les animations et les mises à jour visuelles ; acquisitions maintenues. Les cadrans d’Accueil sont réutilisés entre ticks.

Supervision externe : noms exacts LINK/PERISCOPE/CONTROL/RELAY/HUB, relevé local 30 s, aucune commande de processus, aucun scan de ports ni appel réseau. Exécutables génériques Python/Node non attribués, donc état de service inconnu. Pas de prétention à la santé LIVE. Réseau : interruption de plus de 10 s réamorce le débit, sans volume inventé.
