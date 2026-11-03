# AXIS V0.8 — Vérification des sources pour GitHub

## Périmètre et état initial

Synchronisation demandée du dépôt `shinobione/shino-axis`, branche locale `codex/p1-foundation-lab`, base `5a32a5ce1d50f52e5b438d5fa9b9f162ea14a9de`. La référence distante `main` a été lue au même SHA avant préparation. README/architecture/roadmap modifiés localement et sources/tests/livraisons V0.1–V0.8 non suivis préexistaient à la mission ; conservés et inclus dans la sélection source.

Documentation réécrite à partir du code actuel et des rapports locaux, sans audit général SHINO. CHANGELOG V0.1–V0.8, développement/packaging et qualification Chrome bornée ajoutés. P0/P1 conservés comme historique ; chemins personnels des rapports locaux V0.1–V0.5 remplacés par chemins relatifs, originaux préservés hors Git. Aucun changement fonctionnel du code applicatif ou des tests ; nettoyage des espaces de fin de ligne/fichier préexistants signalés après indexation. Les originaux sont conservés dans la copie isolée.

## Vérification exécutée pour cette synchronisation

Reconstruction dans un dossier isolé à partir de la seule sélection de fichiers destinée à Git ; aucun fichier de profil privé copié. SDK **10.0.403** local et cache de packs NuGet existant utilisés, audit NuGet réseau désactivé (`-p:NuGetAudit=false`). Le cache est un outil de build externe à la sélection, pas une dépendance source ou un profil utilisateur. Une restauration sur machine vierge avec téléchargement réseau n'a pas été exécutée.

- Build Debug `src/Axis.App` : succès, **0 avertissement, 0 erreur**.
- Suite normale `tests/Axis.Core.Tests`, Release : **71 réussites, 0 échec**, sans option de recette privée. Premier passage sandbox : 68 réussites et 3 accès sockets loopback interdits ; relance avec accès loopback autorisé : suite complète réussie.
- `dotnet publish src/Axis.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false` dans une sortie isolée : succès.
- Compilation Release `tests/Axis.App.VisualChecks` : **0 avertissement, 0 erreur**. Ce résultat est une compilation du harness, sans nouvelle recette visuelle.
- Liens relatifs README, CHANGELOG, architecture, roadmap, développement et qualification Chrome : aucune cible manquante.
- `git diff --cached --check` : nettoyage des espaces préexistants des sources nouvellement suivies, puis contrôle final avant commit.
- Comparaison aux sources/tests de la copie reconstruite : aucun changement hors espaces de fin de ligne/fichier ; version fonctionnelle identique à celle testée.

Les journaux frais restent dans `.runtime/github-sync-20261103`, ignoré. Les preuves WPF/animations/cadences détaillées dans V0.8-DELIVERY restent **historiques** ; les cadences ont été mesurées sur sources synthétiques. Aucune nouvelle preuve du compte Codex réel ou de la veille physique n'est revendiquée.

## Publication des sources et exclusions

Sélection explicite : `.gitignore`, README, CHANGELOG, `global.json`, lanceur, `src`, `tests`, documentation et laboratoire historique propre. Aucun binaire, SDK, package tiers téléchargé ou cache redistribué. Les fichiers JSON du laboratoire sont des locks, inventaires de dépendances et résultats **TEST** ; le laboratoire n'est pas une dépendance produit. Les rapports P0 déjà suivis sont conservés, sans ajouter de dump matériel.

Contrôle des chemins et recherche de motifs de credentials effectués sur les candidats : aucun profil `settings/projects/quota-history/state.local/auth`, `.env`, journal JSONL, base privée, ZIP/EXE/DLL/PDB ou clé détectée. Cette recherche ciblée ne remplace pas un audit universel de secrets ; les fixtures et nouvelles preuves documentaires ont aussi été relues pour éviter contenu et identifiants privés.

V0.8 est le nom de livraison affiché par le shell et le lanceur. Les projets ne déclarent pas de propriété MSBuild `Version` de release ; ne pas assimiler les versions d'assemblage par défaut à la numérotation produit. Aucun changement de version binaire artificiel effectué pour cette mission documentaire.

## Limites et opérations non effectuées

Aucun autre dépôt, appareil, firmware, LAN, service ou application historique modifié. Aucun profil réel lu pour cette vérification, aucun nouveau lancement de l'instance utilisateur, aucune acquisition réelle quotas/missions, aucune reconfiguration Chrome, aucun rattrapage historique ChatGPT et aucune GitHub Release binaire.

La recette structurée Chrome rapportée séparément concerne le récepteur AXIS V0.7 et l'extension externe ; son code ne fait pas partie de cette synchronisation. Les parités LINK/PERISCOPE/CONTROL restent partielles. La roadmap distingue livré, à valider, planifié et exploratoire.

Prochaine action produit : recette CODEX V0.8 sur compte réel selon V0.8-DELIVERY ; deux dates de relevés progressant et un tour terminal sans notification dupliquée. Veille physique séparée.


Revue indépendante Advisor avant publication : avis favorable sur les 98 fichiers candidats, sans bloqueur concret identifié ; périmètre public et limites de validation relus. Publication autorisée explicitement par la demande utilisateur ; aucune release binaire autorisée ou préparée.
