# SHINO // AXIS V1.0 — WINDOWS COCKPIT

**One core. Many possibilities.** AXIS est une application Windows native fonctionnelle en .NET 10 / WPF : cinq modules dans un même processus, cockpit, pages détaillées, activation individuelle, préférences et tray Windows.

V1.0 est livrée localement. Ce dépôt fournit les sources pour la reconstruire ; les packages binaires et profils privés sont exclus. La parité complète avec LINK, PERISCOPE et CONTROL n'est pas acquise.

## Modules disponibles

| Domaine | Fonctionnalités et limites |
| --- | --- |
| SYSTEM / PC | CPU Windows, RAM physique utilisée/totale, GPU NVIDIA 0 et température via `nvidia-smi` si disponible. GPU AMD/Intel et température CPU non fournis. |
| NETWORK | Interface active, IPv4, état, débits des compteurs locaux et volumes observés en session. Pas de test de débit Internet. |
| MEDIA | Sélection stable parmi les sessions Windows SMTC : titre, artiste, album, pochette, état lecture/pause et progression si exposés. Lecture seule, sans extrapolation lorsque la vitesse est absente. |
| CODEX | Quotas 5 heures/hebdomadaires lorsque fournis, catégories, jauges OBSIDIAN CIRCLE, dates de reset et historique. Quotas toutes les **30 s** par défaut, réglables à 30/60/120/300 s ; missions JSONL locales toutes les **5 s** nominalement, états, durées et animations WPF. |
| PROJECTS | Import CONTROL prévisualisé/fusionné, projets/sources/preuves, contrôles et affectations manuels, statuts/reprises, collecte structurée ChatGPT par l'extension Chrome existante, déduplication et lecture incrémentale GitHub. |

Les valeurs absentes/périmées restent indisponibles ou à vérifier. Une fin de tour Codex ne termine pas une conversation ou un projet. Notifications de fin optionnelles, désactivées par défaut.

## Compiler, tester et lancer

Prérequis : Windows x64, Windows 10 version 2004 (build 19041) ou ultérieure, Git et **SDK .NET 10.0.403** épinglé dans [global.json](global.json). Accès aux flux de restauration .NET/NuGet nécessaire sur une machine neuve. Aucun Node.js, Python ou Macro Deck requis pour l'application.

Depuis la racine, dans PowerShell :

```powershell
dotnet --version
dotnet build src/Axis.App -c Debug
dotnet run --project tests/Axis.Core.Tests -c Release
dotnet publish src/Axis.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/Axis-V1.0-win-x64
.\Launch-AXIS.cmd
```

Les tests sont un **exécutable de vérification** : utiliser `dotnet run`, pas `dotnet test`. La suite normale utilise des fixtures synthétiques et sockets loopback. Le lanceur ouvre `artifacts/Axis-V1.0-win-x64/Axis.App.exe` : **produire ce dossier après un clone**. Garder tous les fichiers voisins ; aucun runtime .NET séparé requis par le package autonome. La garde V1.0 restaure l'instance existante pour un même profil ; fermer les anciennes versions avant la bascule.

Exécution de développement : `dotnet run --project src/Axis.App`. [Guide de développement et packaging](docs/DEVELOPMENT.md).

## Données et dépendances

Profil par défaut : `%LOCALAPPDATA%\SHINO\AXIS`, avec `settings.json`, `quota-history.json` (1000 points maximum), `projects.json` et sa sauvegarde. `--data-dir <dossier>` isole un profil. Les fichiers invalides sont conservés et protégés contre l'écrasement. Les données projets/preuves peuvent être privées : les garder hors Git.

CODEX nécessite le CLI Codex installé et connecté, détecté par PATH, emplacements prévus ou `SHINO_CODEX_EXE`. Processus `app-server` temporaire pour `account/rateLimits/read`, timeout 20 s, une requête au plus et backoff. Aucun accès direct aux fichiers d'authentification. Missions : métadonnées JSONL locales non documentées et sensibles aux versions ; aucun prompt ou contenu de conversation persisté par ce lecteur.

PROJECTS écoute seulement `127.0.0.1:4187`, dans AXIS. Réception privée et GitHub automatique nécessitent un opt-in ; origine d'extension à configurer. CONTROL CORE conserve 4177, jamais repris automatiquement. L'extension Chrome est **externe et non distribuée ici**. La synchronisation GitHub lit des commits, PR, workflows et signaux documentaires bornés ; elle ne pousse ni ne fusionne du code. Accès privé éventuel via `GITHUB_TOKEN` ou `GH_TOKEN` dans l'environnement, jamais Git. [Architecture et configuration](docs/ARCHITECTURE.md).

## Validations et limites

- V1.0 : **87 tests**, 40 rendus/scénarios WPF sur fixtures, tray/instance unique/fermeture et sauvegarde retardée qualifiés sur profils isolés. CPU/RAM/GPU observés ; musique réelle et veille physique restent à qualifier. [Livraison V1.0](docs/V1.0-DELIVERY.md).
- V0.9 : LINK & MEDIA livré localement ; 79 tests et 16 rendus WPF synthétiques rapportés, validations musicales réelles complémentaires ouvertes. [Livraison historique](docs/V0.9-DELIVERY.md).

- V0.7 : import réel de **28 projets CONTROL, 328 sources, 541 preuves**, avec préservation du projet AXIS existant ; zéro conflit et aucune relation orpheline rapportés.
- Recette Chrome ultérieure : conversation de **527 messages**, attribution correcte et aucune modification persistante après rechargement inchangé. Résultat borné, sans certification de toutes les API internes ChatGPT. [Qualification](docs/CHROME-COLLECTION-VALIDATION.md).
- V0.8 : **71 tests réussis**, build autonome et animations WPF vérifiés dans le rapport de livraison. Cadences 30 s / 5 s mesurées avec des **sources synthétiques** ; requalification sur compte Codex réel et reprise après veille physique encore à effectuer. [Preuves et limites V0.8](docs/V0.8-DELIVERY.md).

LINK, PERISCOPE et CONTROL sont partiellement portés ; aucun retrait automatique de leurs processus. RELAY/HUB, Android TV et SmallTV restent des intégrations futures, avec contrats et autorisations distinctes. Aucun rattrapage historique ChatGPT demandé : rouvrir les conversations nécessaires.

V1.1 Device Integration est la prochaine priorité ; PROJECTS fonctionne déjà, son développement avancé étant reporté à V1.3+. Voir la [roadmap](docs/ROADMAP.md).

## Documentation

- [Architecture actuelle](docs/ARCHITECTURE.md) · [Développement](docs/DEVELOPMENT.md)
- [Historique V0.1 → V1.0](CHANGELOG.md) · [Roadmap](docs/ROADMAP.md)
- [Livraison V0.7](docs/V0.7-DELIVERY.md) · [Bascule/retour](docs/V0.7-SWITCHOVER.md) · [Livraison V0.8](docs/V0.8-DELIVERY.md)
- [Archives P0](docs/audit/P0-RECOMMENDATION.md) · [Archives P1](docs/p1/P1-COMPARISON.md) : résultats historiques, pas des prérequis futurs au lancement.

Licence générale à décider ; cette synchronisation n'accorde aucune nouvelle licence de redistribution. SDK/runtimes Microsoft et dépendances du laboratoire gardent leurs conditions propres. Aucun code tiers téléchargé, runtime, cache ou binaire inclus.

## Cockpit Windows V1.0

Accueil place les missions (jusqu’à quatre) et incidents réels avant les mesures et la musique. Modules et Paramètres regroupent activation persistante, fraîcheur des sources, cadence des quotas, animations, notifications et comportement du tray.

Le démarrage Windows est optionnel et désactivé par défaut : entrée HKCU dédiée `SHINO.AXIS`, créée uniquement après action explicite dans Paramètres. Il est bloqué sur les profils `--data-dir` isolés. Garder le package à son emplacement. Une seule instance par utilisateur/profil ; un second lancement restaure la première. Des profils distincts peuvent coexister pour les tests, avec ports distincts.

La supervision externe observe des noms précis de processus toutes les 30 s : présence ne signifie pas santé ; les services Python/Node génériques restent inconnus. Aucun composant historique n’est commandé.
