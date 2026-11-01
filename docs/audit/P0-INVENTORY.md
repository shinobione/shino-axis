# P0 — Inventaire et preuves

Audit du 2 novembre 2026. Périmètre : sources et métadonnées, aucune exécution métier. Les chemins utilisent `%USERPROFILE%` pour ne pas recopier de nom de compte. Les identifiants de sources ci-dessous sont communs aux cinq rapports.

## Niveaux de preuve

- **L** : fichier ou métadonnée Git local effectivement lu ; prouve seulement cet état source.
- **G** : contenu GitHub effectivement obtenu par le connecteur autorisé ; ne prouve ni checkout ni installation.
- **D** : affirmation documentaire lue, sans nouvelle qualification d'exécution.
- **O** : observation système limitée à l'environnement accessible.
- **H** : hypothèse ou proposition à tester. **N** : inconnu/inaccessible.

Un SHA HEAD ne couvre pas les fichiers modifiés/non suivis. Aucun niveau L/G/D ne vaut preuve physique actuelle.

## Projets et états locaux

| Réf. | Chemin sous `%USERPROFILE%` | Branche / HEAD observés | Architecture et état |
|---|---|---|---|
| A | `SHINO-AXIS` | Pas de dépôt Git reconnu à cette racine | Documentation Foundation ; ni runtime ni choix de langage validés [L : README, AGENTS, docs/ARCHITECTURE, docs/ROADMAP]. |
| T | `Documents/SHINO-TV` | `feature/shino-link-windows-pilot` ; `b323c22efa46364b6f545f3ce86b6e2fe628e48f` | Arbre propre au relevé `git status --short` ; dépôt commun aux worktrees TV/LINK [L]. |
| K | `SHINO-LINK` | HEAD détachée ; `b323c22efa46364b6f545f3ce86b6e2fe628e48f` | Python, psutil, tray pystray/Pillow, envoi Digest ; deux fichiers modifiés : `companion/shino_link.py`, `push_fsless_metrics.py` [L]. |
| S | `SHINO-TV-V06-AUDIT` | `feature/shino-tv-b3-telemetry-scratch-experiment` ; `244334edac07701dc25b5ffc494715152ae1bfa5` | C++/Arduino ESP8266, PlatformIO, LCD 240×240 ; roadmap modifiée et éléments non suivis. Profil par défaut `esp12e`, profils expérimentaux distincts [L]. |
| B | `Documents/SHINO-LAB` | Pas de dépôt Git reconnu à cette racine | Laboratoire Python, validateurs et projections, HUB distribué par modules Python ; candidats systemd Linux [L]. |
| R | `Documents/SHINO-LAB/SHINO-RELAY` | `main` ; `1697b0af2f44fcad2a7be8d599ee382af1e2e1f3` | Python, Tkinter, collecteurs, publication HTTPS ; launcher C#/.NET avec job Windows. Six fichiers suivis modifiés au relevé `status --short -uno` ; non-suivis non inventoriés [L]. |
| V | `Documents/SHINO Television Android` | `main` ; `83e960902c504f8e10b03344dac7f66bebf3002a` | Kotlin/Compose TV, API min 26/cible 35, Java 17 ; source locale **2.1.2/code 10**, modifications et ajouts réseau/news non committés [L]. |
| P | `Documents/SHINO-CODEX-MONITOR` | `codex/obsidian-visual-parity` ; `369aa68294e05e4dd4e6af7d3be17bd26d6350a2` | C#/WinForms, compilation avec csc .NET Framework ; source **5.2.0-preview.1**, nombreux changements suivis et ajouts [L]. |
| C | CONTROL | Checkout non localisé dans les quatre chemins usuels testés | Sources GitHub inspectées ; Node ≥20, modules ESM, serveur HTTP et UI navigateur, collector Chrome MV3 [G]. Pas de conclusion d'absence locale. |
| X | `SHINO-LINK-LIVE`, `SHINO-LINK-MEDIA`, `SHINO-LINK-SCENE`, `SHINO-DEV/SHINO-TV`, `Documents/SHINO-BRAIN` | Répertoires repérés ; accès Git refusé | Branche, SHA et architecture actuelle **N**. BRAIN reste optionnel ; SHINO-OS n'a pas été localisé/inspecté. |

R détail des modifications suivies : `RESUME-LIVE-CODEX.md`, `adapters/now_playing.py`, `relay/__init__.py`, `relay/build_upgrade.py`, `tests/test_build_upgrade.py`, `tests/test_now_playing.py`. Les répertoires privés, caches, reçus, dumps, historiques et configurations réelles ont été exclus de la lecture ciblée.

## GitHub, local, installation : trois identités distinctes

| Projet | GitHub `main` lu via API | Écart local / portée |
|---|---|---|
| AXIS | `91c252dcf3b245338b1d59db98baec340b17566a` | Pas de lien Git démontré avec la copie locale A [G/L]. |
| CONTROL | `f2345788d8f690df6d11b7d27ce2b6d385b04c27` | `package.json` lu : 0.13.0 ; collector 0.2.9 au commit épinglé ; installation N [G]. |
| Android | `ac07be32a755616e7a50ddbcc04d4986aa1670d7` | Build Gradle distant épinglé : **2.0.1/code 4**, distinct du 2.1.2 local [G/L]. |
| PERISCOPE | `3f27c082c132937b3b82a6d2fec2500e9205be8a` | Assembly distante épinglée : **5.1.0**, distincte du preview local [G/L]. |
| SHINO-TV | `8425dd7f2526c29e6c53b4294909a4bf03d3112f` | Ref distante observée uniquement ; contenu non comparé intégralement à T/K/S [G]. |
| RELAY / HUB | Pas de ref distante interrogée | Copies locales et documentation seulement ; version installée non attestée [L/D/N]. |

Sources G : [issue AXIS #1](https://github.com/shinobione/shino-axis/issues/1), branches `/repos/shinobione/{shino-axis,shinobione-shino-control,SHINO-Television-Android,SHINO-CODEX-MONITOR,SHINO-TV}/branches/main` via API GitHub. Fichiers de version Android et PERISCOPE lus aux SHA indiqués. CONTROL README/package/SECURITY lus sur main ; serveur, sécurité HTTP et manifest lus au SHA indiqué.

**Contradiction ouverte [L/D] :** A/ARCHITECTURE annonce RELAY 0.5.7 et HUB 0.5.1 ; R/relay/__init__.py vaut 0.5.5 et R/Launcher.cs porte 0.5.3.0. R/README et SOURCE-CONTRACTS décrivent aussi des étapes historiques 0.4/0.5. Ces identifiants ne permettent pas de déduire le paquet actif. Ne pas remplacer une installation sur cette base.

## Index des sources techniques lues

Les références `R/...`, etc. se résolvent avec la table des chemins. Un symbole est fourni pour retrouver le passage sans parcourir tout le projet.

| Réf. | Fichiers / points d'entrée |
|---|---|
| A | README.md ; AGENTS.md ; docs/ARCHITECTURE.md ; docs/ROADMAP.md ; issue #1 [G]. |
| K | companion/{README.md,shino_link.py,push_fsless_metrics.py,requirements.txt,requirements-link.txt} : constantes de cadence, endpoint, autostart. |
| S | firmware/platformio.ini ; firmware/LICENSE ; firmware/src/boot/{FirstBootBridge.cpp,FslessMetrics.cpp} : routes, `acceptMetrics`, `stale` ; docs/ROADMAP.md. |
| B | README.md ; docs/CONTRACT.md ; requirements.txt ; hub/core.py ; deployment/2c2c/shino-hub.service. |
| R | README.md ; SOURCE-CONTRACTS.md ; docs/REUSED-SOURCES.json ; Launcher.cs ; relay/{__init__,core,sources,codex_live,daily_runtime,daily}.py ; hub/{core,protocol,reliable}.py ; contracts/snapshot-v2.schema.json. Checkpoint RESUME-LIVE-CODEX consulté comme document historique, sans lire ses preuves privées référencées. |
| V | README.md ; app/build.gradle.kts ; contracts/snapshot-v2.schema.json ; docs/NETWORK-PC-INTEGRATION-2.1.md ; core/{HubModels,PcNetwork}.kt ; data/HubClient.kt sous app/src/main/java/fr/shinobiwan/tvhome ; companion/windows/gateway.py. |
| P | README.md ; BUILD-PERISCOPE-V5.2.ps1 ; src/{AssemblyInfo,CodexReader,LocalStore}.cs. |
| C | [README](https://github.com/shinobione/shinobione-shino-control/blob/main/README.md), package.json, SECURITY.md ; au commit ci-dessus : server-entry.mjs, lib/{http-security,state-mutation-queue}.mjs, extension/control-collector/manifest.json. |
| N1 | Node-RED : [package.json](https://github.com/node-red/node-red/blob/main/package.json), blob `43b31f2918aa035af896d6d32c5b7728e627ecc3` ; [LICENSE](https://github.com/node-red/node-red/blob/main/LICENSE), blob `feaf1c9f38c97552ffd55dbf00177445808b8d25`. Lecture documentaire seulement. |

## Empreintes ciblées de la copie locale (SHA-256)

Ces empreintes fixent les divergences critiques, sans archiver de données privées.

| Fichier | SHA-256 |
|---|---|
| R/relay/__init__.py | `956d1614e1c80370d4d880c40c7838285352b87bc00c84f7946f38483438e450` |
| R/relay/daily_runtime.py | `4db0480f623b0d61c12cb6e8375ae87af004827fdd10e39e64d49eb24c0ddba2` |
| R/relay/codex_live.py | `190806ab1ddd5980141272c1fcce11ca1d5a84404a0cd16f7ee058d1bf1dab87` |
| R/contracts/snapshot-v2.schema.json | `8bed0e07943cd23232a7265dcbc6b7ade0bbf708f198c57b75e3f465f38bdd6b` |
| B/hub/core.py | `08005462cf6fc6e32b1acaeb3091d9a07d461fd47793ae4b8db99024af01b4ce` |
| V/app/build.gradle.kts | `2820e2dc3959d7f451b64370679cef1c239bcba95e69f505db66ff3a9f68ede1` |
| V/contracts/snapshot-v2.schema.json | `03fe036cd9d47ba9e28d296bc19a06f26a9b72beed884c33ebec6f24d5deec83` |
| V/core/HubModels.kt | `a8b9845af93672ec8fe412189389914f4b9d2a7e413f08225e7a714ad562d238` |
| P/src/CodexReader.cs | `7b1b54629d9cbc905db7f8deb576c57261f886ae3d71dca3f139cbf9a89efdea` |
| K/companion/shino_link.py | `09989fb522760b25d5c600620fe93c79c93c3f7b97bec29041173e14dc071f38` |
| S/firmware/src/boot/FslessMetrics.cpp | `30b7d714ea6abb60aea5114dcbf7aa17b2bfa13c69d3fd7bc771365026e45136` |

## Observations et limites d'installation

`Get-Process` filtré sur SHINO/PERISCOPE/Python/Node/Codex n'a retourné aucune ligne dans la vue accessible [O]. Cela ne prouve pas l'absence de processus sur le bureau hôte. `Get-Service` a échoué à ouvrir le Service Control Manager [O/N] ; aucun service Windows installé n'est donc attesté. Pas de baseline CPU/RAM exploitable, d'inventaire de ports actifs ni de vérification de l'autostart réel.

Aucune connexion HUB, VM, SmallTV ou Freebox ; version installée, firmwares, état de publication et santé actuelle **N**. Les validations anciennes mentionnées par les documents restent D. Voir [risques](P0-RISKS.md) et [protocole P1](P0-RECOMMENDATION.md).
