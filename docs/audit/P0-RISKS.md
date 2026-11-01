# P0 — Risques et inconnues

Niveaux et références : [inventaire](P0-INVENTORY.md). Les conditions ci-dessous sont des critères pour une mission future ; aucune opération de levée de risque n'est exécutée ici.

| Priorité | Risque / constat | Preuve et conséquence | Condition de maîtrise |
|---|---|---|---|
| P0 | Confondre GitHub, checkout et installation | L/G : Android 2.0.1 distant vs 2.1.2 local ; PERISCOPE 5.1 distant vs preview local ; RELAY/HUB annoncés différents des fichiers. N : installations. | Garder une identité par classe ; aucune migration tant que le build actif n'est pas identifié sous accès précis. |
| P0 | Producteurs/acquisitions concurrents | L : R daily quotas DIRECT et P RPC ; K et R acquièrent PC. N : coexistence réelle. | Deux sources synthétiques en P1 ; ne démarrer aucun collecteur réel. Toute future bascule retire l'autorité précédente de façon contrôlée, jamais par simple ajout. |
| P0 | Perte de protections durables | L/D : R/reliable, daily et SOURCE-CONTRACTS ont verrous, watermarks, HOLD et issues inconnues. | Préserver UNKNOWN_NO_RETRY, intentions et autorité ; aucun effacement, reset, copie d'ancien état ou replay « pour repartir ». |
| P0 | Exposition de CONTROL / conversations | G : C/SECURITY et http-security ; état privé, sécurité loopback et extension isolée. | Pas de proxy LAN ni d'ingestion de conversations dans AXIS. Utiliser une projection minimale synthétique en P1. |
| P1 | Schémas locaux divergents | L : V/schema/network accepté ; R/schema fermé sans network. D : note réseau encore « proposée ». | Matrice explicite des révisions et tests croisés ; émetteur inchangé avant qualification du lecteur/validateur. |
| P1 | Faux LIVE / données rajeunies | L/D : TTL 6/10/900, dates source et états d'absence ; grâce d'affichage V distincte de fraîcheur. | Horloge injectée, cas de seuil/reset/retard ; jamais de substitution generatedAt→capturedAt. |
| P1 | Surdimensionner SmallTV | L : ESP8266, métriques RAM, corps borné, protocole distinct ; N : firmware actuel et chemin média récent. | Client simulé borné ; maintien du firmware ; aucune promesse HUB direct/WebSocket/JSON volumineux. |
| P1 | Supprimer la VM/HUB trop tôt | L : candidats systemd, chaîne HTTPS ; N : configuration active, services autonomes PC éteint. | Conserver HUB et éventuelles sources autonomes ; pas de remplacement réseau en P0/P1. |
| P1 | Licence ou notices insuffisantes | L : S/firmware/LICENSE GPLv3, SPDX GPL-3.0-or-later dans FslessMetrics ; A/README licence à décider ; N : audit transitif des autres composants. | Réutiliser par interface d'abord. Inventorier licences, notices et versions exactes avant copie/distribution ; aucune compatibilité juridique globale conclue. |
| P1 | Résultats de performance inventés | O : vue processus vide après filtre ; SCM inaccessible ; aucune mesure CPU/RAM utile. | Protocole identique, processus enfants inclus, mesures exportables ; « non mesuré » tant que non exécuté. |
| P2 | Protocole d'activité non stable | D : P/README indique JSONL non documenté et UNVERIFIED ; pas d'export mission vérifié prouvé. | Découpler activité et quotas ; cas manquant/ambigu n'est pas une fin confirmée. |
| P2 | Module arbitraire trop puissant | H : hôte généraliste, scripts/plugins natifs et passerelle pourraient élargir les droits. | Deux sources/deux clients d'abord ; modules connus, droits explicites, aucun marketplace ni script libre dans Foundation. |

## Limites d'accès et informations encore manquantes

- **Accès refusé [N]** : worktrees LINK récents, SHINO-DEV/SHINO-TV et SHINO-BRAIN. Aucun contournement ni demande d'élévation. Architecture BRAIN actuelle, contrats média SmallTV récents et SHA correspondants non certifiés.
- **Non localisé [N]** : checkout CONTROL dans les chemins usuels testés, SHINO-OS. Le GitHub CONTROL lu permet une analyse de ses frontières, pas une comparaison locale exhaustive.
- **SCM inaccessible [O/N]** : noms/états des services Windows, paramètres réels d'autostart, chemins des exécutables actifs et ports en écoute non attestés. Les sources décrivent surtout des applications tray/supervisors et un service Linux.
- **Installations non inspectées [N]** : Android, SmallTV, HUB distant, paquet RELAY et PERISCOPE actifs. Ni preuve physique actuelle ni comparaison binaire/source.
- **Non mesuré [N]** : CPU/RAM au repos, veille/reprise, redémarrage, réseau instable, installation/désinstallation. Les anciens nombres de tests dans les README ne sont pas des résultats P0.
- **Licences incomplètes [N]** : pas de nomenclature transitive ni validation des conditions des bibliothèques PyWinRT, Pillow, psutil, composants Android et binaires embarqués. N1 apporte une licence racine de candidat, pas un visa sur tout son écosystème.

## Vérifications P0 et opérations exclues

Effectué : lecture ciblée des documents/code, `git branch --show-current`, `git rev-parse HEAD`, `git status --short` (R/X avec `-uno`), SHA-256 de onze fichiers de code/contrat, lectures GitHub GET autorisées, interrogation système limitée. Échecs d'accès consignés, pas extrapolés.

Tests applicatifs, build, benchmark et tests matériels : **non exécutés**. Seule la cohérence documentaire des cinq livrables est vérifiée à la livraison. Les erreurs de lecture de chemins ont été corrigées sans exécution d'application.

Non effectué : installation/mise à jour, lancement métier, accès aux credentials ou données privées, lecture d'historiques Codex/conversations, modification tierce, configuration/registre/service, réseau local/SSH/ADB, VM/HUB, flash/OTA/reboot, collecte matérielle, reset/recovery, commit/push/merge/PR/publication. Écriture limitée aux cinq fichiers `docs/audit/P0-*.md` d'AXIS.
