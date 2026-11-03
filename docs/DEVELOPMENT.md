# AXIS — Développement et packaging V1.0

## Prérequis

Windows x64, Windows 10 build 19041 minimum ; Git et SDK **.NET 10.0.403** (voir `global.json`). Installer le SDK Windows x64 depuis la distribution Microsoft ou utiliser un SDK local de même version. Le runtime seul ne compile pas. Le SDK historique dans `lab/p1/.cache/dotnet` est un cache ignoré, pas une dépendance du produit.

L'application utilise les références officielles .NET, Windows Desktop/Windows SDK et ASP.NET Core ; aucune dépendance NuGet applicative supplémentaire n'est déclarée. La restauration peut télécharger les packs Microsoft manquants. Aucun accès aux profils CONTROL/Codex n'est requis pour compiler ou exécuter les tests synthétiques.

## Commandes depuis la racine

```powershell
dotnet --version
dotnet build src/Axis.App -c Debug
dotnet run --project tests/Axis.Core.Tests -c Release
dotnet publish src/Axis.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/Axis-V1.0-win-x64
```

La suite est un exécutable, pas un projet de découverte `dotnet test`. Elle emploie des profils temporaires et ports loopback de test. Ne pas employer ses options `--live-github`, `--control-preview` ou `--control-compare` pour la validation ordinaire : elles désignent des recettes distinctes. Le harness WPF est séparé (`tests/Axis.App.VisualChecks`) ; ne pas confondre sa simulation avec le profil réel.

Exécution développement :

```powershell
dotnet run --project src/Axis.App -- --data-dir .runtime/dev-profile --projects-port 14187
```

Un profil neuf active les cinq modules par défaut ; cette commande peut donc lancer les collectes locales et le CLI Codex connecté. La compilation et la suite normale ne lancent pas ce profil. La garde V1.0 restaure la première instance pour un même profil ; des profils distincts nécessitent des ports distincts si PROJECTS est actif.

## Distribution locale autonome

Après `publish`, lancer `Launch-AXIS.cmd` ou `artifacts/Axis-V1.0-win-x64/Axis.App.exe`. Le lanceur transmet les arguments. Conserver **le dossier entier** ; pas de single-file ni trimming.

Archive facultative locale :

```powershell
Compress-Archive -Path artifacts/Axis-V1.0-win-x64/* -DestinationPath artifacts/AXIS-V1.0-Windows-x64.zip
```

Ne pas écraser un livrable historique qualifié pendant une simple vérification : utiliser un autre dossier de sortie et un autre nom d'archive. Les empreintes des rapports historiques concernent leurs binaires d'origine, pas une reconstruction ultérieure. ZIP/EXE/DLL restent ignorés ; aucune GitHub Release binaire prévue par cette mission.

## Conventions et exclusions

- Noyau/modèles indépendants de WPF, adaptateurs Windows au niveau approprié, modules compilés dans le registre existant.
- Données absentes/inconnues/périmées explicites ; conserver fraîcheur, limites et provenance.
- Garder le récepteur loopback, sans reprendre automatiquement CONTROL 4177 ni lancer d'anciens producteurs SHINO.
- Fixtures synthétiques dans `tests`, aucun profil réel, token, transcript, journal JSONL ou dump matériel dans Git.
- `bin`, `obj`, `.runtime`, `artifacts`, caches et sources tierces téléchargées restent hors publication.

`lab/p1` est un laboratoire historique distinct. Son bootstrap et ses dépendances servent à reproduire P1, **pas à compiler AXIS**. Voir son README pour les conditions et limites de redistribution. Licence générale AXIS encore à décider.

## Vérification V1.0

Le harness `tests/Axis.App.VisualChecks` peut produire les 32 rendus de pages et huit scénarios synthétiques avec
`--output=artifacts/v10-layout --data-dir .runtime/v10-visual-fixtures-only` (profil isolé impératif).
`--windows-probe` sonde PC et SMTC en lecture seule, sans journaliser les titres.
Il distingue absence de session et accès SMTC refusé. La reprise est simulée par réamorçage CPU.
`tests/Measure-AxisProcess.ps1` mesure une instance temporaire avec un nouveau profil explicite ;
CODEX et PROJECTS sont désactivés, PC/NETWORK/MEDIA seuls actifs. Ne pas utiliser un profil utilisateur.

## Recette V1.0 isolée

Le harness utilise une `Application` de test neutre chargeant les styles produit, sans démarrage implicite de fenêtre/collecteur. Depuis la racine, lancer le harness construit avec `--lifecycle --data-dir .runtime/v10-life-UNIQUE --projects-port 44190` sur un dossier neuf : CODEX/PC/MEDIA désactivés, seul PROJECTS écoute le port de test. Il vérifie le menu tray réel, la persistance retardée, les courses de fermeture et l’exclusion interprocessus. `tests/Verify-AxisPackage.ps1` valide ensuite le package avec tous les collecteurs désactivés et vérifie que l’entrée autostart réelle reste identique.

Si le SDK système est absent, le cache local historique `lab/p1/.cache/dotnet/dotnet.exe` peut servir avec `DOTNET_CLI_HOME=lab/p1/.cache/cli-home` et `NUGET_PACKAGES=lab/p1/.cache/nuget`. Ce cache reste optionnel, ignoré et non distribué. La compilation WPF et les sockets loopback ont nécessité le contexte Windows hors bac à sable pendant cette recette.
