# AXIS — Roadmap produit

État documentaire au 03/11/2026. AXIS V1.0 est une application Windows .NET 10/WPF livrée localement. Les numéros futurs sont des jalons proposés, sans engagement de date ni de livraison.

| Jalon | Statut | Périmètre |
| --- | --- | --- |
| V0.7 — FOUNDATION + PROJECTS | **LIVRÉ** | Socle Windows, cinq modules, import CONTROL réel et PROJECTS natif. Collecte structurée Chrome qualifiée ensuite sur un cas réel ; parité exhaustive non certifiée. |
| V0.8 — CODEX Live | **LIVRÉ / À VALIDER en réel** | Quotas 30 s configurables, missions 5 s nominales, états/durées/animations et historique dédupliqué. Cadences synthétiques documentées. |
| V0.9 — LINK & MEDIA | **LIVRÉ LOCAL / recette musicale réelle à compléter** | Acquisition PC robuste, sélection SMTC stable, métadonnées/pochette cohérentes, progression prudente, snapshots réutilisables et cockpit compact. |
| V1.0 — Windows Cockpit | **VERSION COURANTE / LIVRÉ LOCAL** | Accueil consolidé, paramètres/modules, instance unique et tray, autostart optionnel, supervision légère en lecture seule. Aucun retrait des processus historiques. |
| V1.1 — Device Integration | **PLANIFIÉ — prochaine priorité** | Intégration Freebox Pop/Android TV et SmallTV 240×240 ; contrats, TTL et migration graduelle avec autorisations distinctes. |
| V1.2 — Extensions et BRAIN | **EXPLORATOIRE** | Nouveaux modules, BRAIN et automatisations locales, sans plateforme marketplace prématurée. |
| V1.3+ — PROJECTS / CONTROL avancé | **REPORTÉ VOLONTAIREMENT** | Parité utile CONTROL, résumés/reprises et opérations GitHub avancées ; aucune modification dans V1.0. |

PROJECTS fonctionne déjà en V1.0 ; son développement avancé est reporté volontairement et ne constitue plus la prochaine priorité.

## À valider

- CODEX V0.8 : deux relevés sur compte réel et un tour terminal observé, puis reprise après veille physique. Ces vérifications sont distinctes des mesures synthétiques et chemins de cycle de vie déjà testés.
- PROJECTS : stabilité prolongée de la collecte structurée, autres formats/onglets et heuristiques ; parité utile à définir et qualifier avant retrait de CONTROL.
- Intégrations : contrats, fraîcheur/TTL, états inconnus, absence de doubles producteurs, retour arrière et accord distinct avant bascule de service ou appareil.

**Aucun chantier actif de rattrapage historique ChatGPT.** L'utilisateur préfère rouvrir les conversations nécessaires. Les endpoints de compatibilité bornés existants restent documentés, sans lancer ni planifier de rattrapage global.

## Historique préservé

[CHANGELOG](../CHANGELOG.md) et rapports V0.1–V1.0 décrivent la progression. [P0](audit/P0-RECOMMENDATION.md), [P1](p1/P1-COMPARISON.md), [ADR historique](adr/ADR-001-FOUNDATION.md) et `lab/p1` conservent leurs résultats et limites. Leur HOLD initial ne décrit plus l'architecture actuelle ; .NET/WPF a été adopté pour V0.1 sans prétendre avoir gagné un benchmark.

Prochaine action concrète : recette MEDIA avec un lecteur réel (lecture, pause, changement de morceau puis fermeture). Réussite = titre/pochette cohérents, chronomètre arrêté en pause, absence de données résiduelles à la fermeture. La veille physique et les autres lecteurs restent des qualifications séparées. Voir [livraison V0.9](V0.9-DELIVERY.md).
