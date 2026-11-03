# AXIS — Roadmap produit

État documentaire au 03/11/2026. AXIS V0.8 est une application Windows .NET 10/WPF livrée localement. Les numéros futurs sont des jalons proposés, sans engagement de date ni de livraison.

| Jalon | Statut | Périmètre |
| --- | --- | --- |
| V0.7 — FOUNDATION + PROJECTS | **LIVRÉ** | Socle Windows, cinq modules, import CONTROL réel et PROJECTS natif. Collecte structurée Chrome qualifiée ensuite sur un cas réel ; parité exhaustive non certifiée. |
| V0.8 — CODEX Live | **LIVRÉ / À VALIDER en réel** | Quotas 30 s configurables, missions 5 s nominales, états/durées/animations et historique dédupliqué. Cadences synthétiques documentées. |
| V0.9 — PROJECTS / CONTROL Parity | **PLANIFIÉ — prochaine étape envisagée** | Parité utile CONTROL, résumés et reprises de contexte, fiabilisation des opérations GitHub, parcours de gestion, validation avant remplacement de l'interface CONTROL. |
| V1.0 — Windows Cockpit | **PLANIFIÉ** | Parité utile LINK, consolidation des réglages/modules, expérience Windows cohérente, supervision des composants nécessaires ; réduction des processus redondants après validation. |
| V1.1 — Device Integration | **PLANIFIÉ** | Freebox Pop/Android TV, SmallTV 240×240, contrats existants, commandes d'affichage et sélection des vues. Firmware SmallTV stable préservé ; migration graduelle RELAY/HUB seulement sur preuves de parité. |
| V1.2+ — Extensibility | **EXPLORATOIRE** | SHINO // BRAIN, nouveaux modules, automatisations locales et intégrations futures. |

## À valider

- CODEX V0.8 : deux relevés sur compte réel et un tour terminal observé, puis reprise après veille physique. Ces vérifications sont distinctes des mesures synthétiques et chemins de cycle de vie déjà testés.
- PROJECTS : stabilité prolongée de la collecte structurée, autres formats/onglets et heuristiques ; parité utile à définir et qualifier avant retrait de CONTROL.
- Intégrations : contrats, fraîcheur/TTL, états inconnus, absence de doubles producteurs, retour arrière et accord distinct avant bascule de service ou appareil.

**Aucun chantier actif de rattrapage historique ChatGPT.** L'utilisateur préfère rouvrir les conversations nécessaires. Les endpoints de compatibilité bornés existants restent documentés, sans lancer ni planifier de rattrapage global.

## Historique préservé

[CHANGELOG](../CHANGELOG.md) et rapports V0.1–V0.8 décrivent la progression. [P0](audit/P0-RECOMMENDATION.md), [P1](p1/P1-COMPARISON.md), [ADR historique](adr/ADR-001-FOUNDATION.md) et `lab/p1` conservent leurs résultats et limites. Leur HOLD initial ne décrit plus l'architecture actuelle ; .NET/WPF a été adopté pour V0.1 sans prétendre avoir gagné un benchmark.

Prochaine action concrète : recette utilisateur CODEX V0.8 sur compte réel. Réussite = dates de deux relevés progressant, tour terminal explicable, arrêt de l'anneau et absence de notification dupliquée. La veille physique reste une vérification séparée.
