# SHINO // AXIS

**One core. Many possibilities.**

> **Statut :** Foundation / Discovery. Aucune technologie ni architecture d'exécution n'est encore validée.

SHINO // AXIS est un projet de **plateforme modulaire Windows-first** qui réunira progressivement les logiciels, les données et les appareils de l'écosystème SHINO dans une seule expérience cohérente.

**AXIS n'est pas une réécriture générale.** Il doit fédérer les programmes qui fonctionnent déjà, standardiser les échanges et permettre d'ajouter de nouvelles capacités sans reconstruire un logiciel entier à chaque idée.

## Objectifs

- **Un produit Windows unique** : interface, tray, installation, configuration, superviseur et diagnostics communs.
- **Des modules extensibles** : fonctions internes et services externes, sans monolithe obligatoire.
- **Des contrats réutilisables** : source, état, événement, commande, capacité, droits et versions.
- **Des appareils connectés** : Android TV, SmallTV 240×240 et futurs clients.
- **Sans VM obligatoire** pour héberger le runtime et la passerelle sous Windows.
- **Préserver les acquis** : intégration par adaptateurs avant toute migration ciblée.
- **Local-first** : données authentiques, provenance, résilience, permissions minimales.

## Écosystème existant

| Projet | Responsabilité | Direction envisagée |
| --- | --- | --- |
| [SHINO // CONTROL](https://github.com/shinobione/shinobione-shino-control) | Projets, GitHub, ChatGPT, reprise des travaux | Conserver la logique métier existante |
| SHINO // LINK | Télémétrie PC et musique | Producteur de données / adaptateur |
| [PERISCOPE / CODEX MONITOR](https://github.com/shinobione/SHINO-CODEX-MONITOR) | Quotas et activité Codex vérifiée | Réutiliser le collecteur fiable |
| SHINO // RELAY et HUB | Publication, validation, distribution | Maintenir les contrats et migrer progressivement |
| [SHINO Television Android](https://github.com/shinobione/SHINO-Television-Android) | Dashboard Freebox Pop | Conserver comme client natif Android |
| [SHINO // TV](https://github.com/shinobione/SHINO-TV) | Firmware SmallTV | Conserver comme client embarqué |
| SHINO // BRAIN | Notes et index mémoire local | Service optionnel |
| [SHINO-OS](https://github.com/shinobione/shino-os) | Assistant IA local | Fournisseur de capacités optionnel |

Cette liste décrit **des intégrations potentielles**, pas des migrations approuvées. Les sources GitHub, les worktrees locaux et les appareils physiques peuvent présenter des états différents.

## Architecture conceptuelle

```text
SHINO // AXIS — Windows
 ├── Interface unifiée + tray
 ├── Runtime / gestion des modules
 ├── Contrats Sources / States / Events / Commands / Capabilities
 ├── Adaptateurs vers les projets SHINO existants
 ├── Passerelle réseau sécurisée et versionnée
 └── Stockage, configuration, diagnostics
               │
               ├── Android TV
               ├── SmallTV
               └── Futurs appareils
```

**La pile technique reste ouverte** : .NET, Rust, Go, Tauri, frameworks natifs ou plateformes open source. On choisira après un laboratoire comparatif fondé sur les intégrations réelles, la sécurité, la maintenance, la licence et les ressources consommées.

## Règles de départ

1. Pas de changement en production, flash, OTA, suppression de VM, modification réseau ni déploiement sans autorisation spécifique.
2. Pas de collecteurs concurrents sur une même donnée ; une source autoritaire par domaine.
3. Préserver les contrats existants du HUB et d'Android TV, notamment authentification et fraîcheur des données.
4. Ne pas exposer directement au réseau le service loopback de CONTROL.
5. Aucune donnée LIVE fictive : les informations absentes, périmées ou non vérifiées restent identifiées.
6. Les fonctions ordinaires ne dépendent pas d'un modèle IA, d'une VM ou d'un abonnement cloud.
7. Pas de système de plugins disproportionné avant démonstration d'une vraie extension.
8. Aucune obligation de fusionner tous les logiciels SHINO.

## Roadmap

1. **P0 — Audit** : relever le code et les versions réellement utilisés, les processus, les dépendances, les protocoles et les rôles.
2. **P1 — Laboratoire comparatif** : tester une base Windows minimale contre une plateforme open source existante, avec les mêmes scénarios.
3. **P2 — Foundation** : interface unique, contrats minimaux et adaptateurs en lecture seule.
4. **P3 — Communication** : compatibilité Android TV / SmallTV et passerelle native Windows.
5. **P4 — Migration** : remplacer les dépendances redondantes, un domaine à la fois, avec retour arrière.
6. **P5 — Extension** : démontrer l'ajout d'un nouveau module sans modification profonde du noyau.

**Premier jalon :** prendre une décision d'architecture documentée sur la base d'un audit en lecture seule et de tests reproductibles, sans casser les installations en place.

## Documents

- [Architecture proposée](docs/ARCHITECTURE.md)
- [Roadmap détaillée](docs/ROADMAP.md)
- [Instructions de travail pour Codex et agents](AGENTS.md)

## Licence

**À décider.** Le dépôt est public mais aucune licence de réutilisation n'a encore été accordée. La licence sera choisie après vérification des composants open source éventuellement repris.
