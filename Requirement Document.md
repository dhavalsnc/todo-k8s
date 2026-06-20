# Comprehensive Documentation
## NAGP 2026 — Kubernetes, DevOps & FinOps Home Assignment

**Submitted by:** Dhaval Chandnani  
**Date:** June 2026  
**Repository:** https://github.com/dhavalsnc/todo-k8s  
**Docker Hub:** https://hub.docker.com/r/dclearning/todo-api

---

## Table of Contents

1. [Requirement Understanding](#1-requirement-understanding)
2. [Assumptions](#2-assumptions)
3. [Solution Overview](#3-solution-overview)
4. [Justification for the Resources Utilized](#4-justification-for-the-resources-utilized)

---

## 1. Requirement Understanding

### 1.1 Core Objective

The assignment requires designing, containerizing, and deploying a **multi-tier application** on Kubernetes that simulates a real-world service topology: a stateless API tier that serves data fetched from a stateful database tier. The system must be production-grade in its configuration practices and demonstrate key Kubernetes capabilities.

### 1.2 Service API Tier Requirements

| Requirement | Details |
|---|---|
| Expose an API endpoint | HTTP endpoint that retrieves records from the database |
| Fetch data from the database tier on invocation | Every API call reads live data from SQL Server |
| Use best practices for DB connectivity | Connection pooling, configuration separation from code |
| Support rolling updates | Zero-downtime deployments via `RollingUpdate` strategy |
| Be externally accessible | Exposed via Kubernetes Ingress (not NodePort or LoadBalancer directly) |
| Demonstrate self-healing | Kubernetes restarts failed pods automatically |
| Demonstrate HPA | Auto-scale pods based on CPU and memory utilization |
| Run 4 replicas | Minimum of 4 pods at any time |
| Use ConfigMap | DB host, database name, pool settings injected via ConfigMap |
| Use Secrets | DB password injected via Kubernetes Secret (never plaintext in YAML) |

### 1.3 Database Tier Requirements

| Requirement | Details |
|---|---|
| One table with 5–10 records | `TodoItems` table seeded with 5 records |
| Data persistence | Data must survive pod deletion and rescheduling |
| Accessible only within the cluster | ClusterIP Service only — no external exposure |
| Automatic recovery after pod deletion | Kubernetes Deployment controller recreates the pod |
| Run 1 replica | Single database pod |
| Use Secrets | SA password stored in a Kubernetes Secret |

### 1.4 Kubernetes Feature Requirements Matrix

| Feature | Service API Tier | Database Tier | Implemented |
|---|---|---|---|
| Exposed outside the cluster | Yes | No | Ingress (AGIC) for API; ClusterIP for DB |
| Number of pods | 4 | 1 | 4 API replicas; 1 MSSQL replica |
| Rolling updates support | Yes | No | `RollingUpdate` for API; `Recreate` for DB |
| Persistent storage | No | Yes | Azure File PV/PVC for DB |
| Configurable via ConfigMap | Yes | Optional | ConfigMap for both tiers |
| Secrets usage | Yes | Yes | Opaque Secrets for both tiers |

### 1.5 FinOps Requirements

- Define CPU and memory requests and limits for the API tier
- Identify at least **three cost optimization opportunities**
- Implement resource optimization based on observed metrics (right-sizing, HPA)

### 1.6 Other Constraints

- Database connection password must **not** appear in plaintext in any YAML file
- Pod IPs must **not** be used for inter-tier communication — only DNS service names
- Database data must survive pod deletion (persistent volume required)
- External access must be via **Ingress**, not a raw LoadBalancer Service

---

## 2. Assumptions

### 2.1 Infrastructure

- The deployment target is **Azure Kubernetes Service (AKS)** with the **Application Gateway Ingress Controller (AGIC)** add-on enabled. The ingress annotation `kubernetes.io/ingress.class: azure/application-gateway` is specific to AKS; a different controller (nginx, traefik) would require a different annotation.
- An **Azure Storage Account** has been provisioned with a file share named `todo-mssql-file-share`. Credentials for this account are stored as the `azure-secret` Kubernetes Secret.
- The AKS cluster has **Metrics Server** installed, which is required for the HorizontalPodAutoscaler to read CPU and memory utilization. AGIC environments typically include it, but it was verified to be present before enabling HPA.
- A single AKS node pool is assumed. Multi-pool or spot-node considerations are out of scope for this assignment.

### 2.2 Application

- The Todo API is a **read-only demonstration application** — it does not need POST/PUT/DELETE endpoints for this assignment. The `GET /todoitems` endpoint is sufficient to prove end-to-end connectivity through the stack.
- SQL Server **Developer Edition** is acceptable. This edition is free and functionally equivalent to Enterprise for development and testing use, with a license restriction against production use.
- The database schema and seed data are initialized manually via `kubectl exec` after the first deployment. There is no automatic migration on startup; this is intentional to avoid startup-time complexity.
- The API version (`2.0.0`) is read from `appsettings.json` and returned in every response alongside the data, allowing version identification at the HTTP level.

### 2.3 Security

- Kubernetes Secrets are stored as **base64-encoded Opaque Secrets**. This is the standard Kubernetes mechanism and is sufficient for an assignment context. In a production environment, secrets would be managed through Azure Key Vault with the Secrets Store CSI Driver or External Secrets Operator.
- The `azure-secret` (storage account key) is included in the repository for demonstration purposes but is treated as environment-specific credentials. The cluster would be torn down after evaluation, and the storage account key would be rotated.
- The MSSQL pod runs as non-root user (`runAsUser: 10001`) to follow security best practices.

### 2.4 Network

- `host: todo-api.local` is used in the Ingress rule as a representative hostname. In practice, the AKS Application Gateway is assigned a public IP (`20.204.212.246`) which is used for direct access.
- All inter-service communication uses **Kubernetes DNS service names** (e.g., `todo-mssql-svc`), never Pod IPs, which change on every pod restart.

### 2.5 FinOps

- Resource requests and limits were set based on **observed behavior** during local Docker Compose development and initial AKS deployment, not load-test data. They represent reasonable starting values for a light-traffic demonstration workload.
- HPA minimum replicas are set to **4** (matching the Deployment's `replicas: 4`) to satisfy the assignment requirement that the API tier always runs 4 pods.

---

## 3. Solution Overview

### 3.1 Technology Stack

| Layer | Technology | Version |
|---|---|---|
| API Framework | ASP.NET Core Minimal API | .NET 10.0 |
| ORM / DB Driver | Entity Framework Core (SQL Server) | 9.x |
| Database | Microsoft SQL Server | 2022 (Developer Edition) |
| Container Runtime | Docker | Latest |
| Orchestration | Kubernetes on AKS | 1.29+ |
| Ingress Controller | Azure Application Gateway (AGIC) | AKS add-on |
| Persistent Storage | Azure Files (CSI Driver) | file.csi.azure.com |
| Local Development | Docker Compose | v3.8 |

### 3.2 Architecture

```
External Client (Internet)
         │
         ▼  HTTP  (Public IP: 20.204.212.246)
 ┌─────────────────────────────┐
 │  Azure Application Gateway  │  ← Ingress (AGIC)
 │  Ingress: todo-api-ingress  │
 └─────────────┬───────────────┘
               │  HTTP :80
               ▼
 ┌─────────────────────────────┐
 │  todo-api-svc (ClusterIP)   │  ← Kubernetes Service (DNS: todo-api-svc)
 └──────┬──────┬──────┬────────┘
        │      │      │  (load balanced across pods)
        ▼      ▼      ▼
  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐
  │ Pod1 │ │ Pod2 │ │ Pod3 │ │ Pod4 │  ← todo-api Deployment (4 replicas)
  └──────┘ └──────┘ └──────┘ └──────┘  HPA scales 4→10 on CPU/Mem > 80%
               │
               │  TCP :1433  (DNS: todo-mssql-svc)
               ▼
 ┌─────────────────────────────┐
 │  todo-mssql-svc (ClusterIP) │  ← Kubernetes Service (internal only)
 └─────────────┬───────────────┘
               │
               ▼
 ┌─────────────────────────────┐
 │  todo-mssql Pod ×1          │  ← MSSQL Deployment (Recreate strategy)
 └─────────────┬───────────────┘
               │  /var/opt/mssql
               ▼
 ┌─────────────────────────────┐
 │  Azure File Share (5Gi PV)  │  ← PV + PVC + StorageClass (Retain policy)
 └─────────────────────────────┘
```

### 3.3 Kubernetes Objects Inventory

| Object Kind | Name | Purpose |
|---|---|---|
| Namespace | `todo` | Logical isolation for all application resources |
| Deployment | `todo-api` | Manages 4 API pod replicas; RollingUpdate strategy |
| Deployment | `todo-mssql` | Manages 1 database pod; Recreate strategy |
| Service | `todo-api-svc` | ClusterIP; load-balances HTTP:80 across API pods |
| Service | `todo-mssql-svc` | ClusterIP; exposes MSSQL TCP:1433 to API pods only |
| Ingress | `todo-api-ingress` | Routes external HTTP traffic to the API service via AGIC |
| ConfigMap | `todo-api-config` | Injects DB connection string (minus password) into API pods |
| ConfigMap | `todo-mssql-config` | Injects `ACCEPT_EULA=Y` and `MSSQL_PID=Developer` |
| Secret | `todo-api-secret` | Injects `DB_PASSWORD` (base64 encoded) into API pods |
| Secret | `todo-mssql-secret` | Injects `MSSQL_SA_PASSWORD` (base64 encoded) into DB pod |
| Secret | `azure-secret` | Azure storage account name + key for the CSI driver |
| StorageClass | `azurefile-storage-class` | Azure File CSI driver; `Retain` reclaim policy |
| PersistentVolume | `todo-mssql-pv` | Manual 5Gi PV bound to the Azure File share |
| PersistentVolumeClaim | `todo-mssql-pvc` | Claims the PV; mounted at `/var/opt/mssql` in DB pod |
| HorizontalPodAutoscaler | `todo-api-hpa` | Scales API pods 4→10 when CPU or memory exceeds 80% |

### 3.4 Application Layer

The API is a **.NET 10 ASP.NET Core Minimal API** with two endpoints:

- `GET /` — Returns the API version from configuration.
- `GET /todoitems` — Queries the `TodoItems` table via Entity Framework Core and returns all records as JSON.

**Database connection assembly at runtime:**

```
DB_CONNECTION_STRING (from ConfigMap)  +  DB_PASSWORD (from Secret)
        ↓                                        ↓
"Server=todo-mssql-svc;Database=todo;...Pooling=true;"  +  "Password=todo@123;"
                          ↓
              Final connection string passed to EF Core
```

This pattern ensures the password never appears in a ConfigMap (plaintext) and never appears in application source code.

**Connection pooling** is configured directly in the connection string:
- `Min Pool Size=5` — maintains 5 idle connections, reducing latency for frequent requests
- `Max Pool Size=10` — caps connections to avoid overwhelming a single SQL Server instance
- `Connection Timeout=30` — prevents indefinite hangs on startup or transient network issues

### 3.5 Database Layer

SQL Server 2022 Developer Edition is deployed in a single pod. The `TodoItems` table is seeded with 5 records:

| Id | Title | Body | CreatedAt |
|---|---|---|---|
| 1 | Buy groceries | Milk, Bread, Eggs, Butter | 2024-06-01 |
| 2 | Call Mom | Check in and see how she is doing | 2024-06-02 |
| 3 | Finish project report | Complete the final report... | 2024-06-03 |
| 4 | Workout | Go to the gym for a workout session | 2024-06-04 |
| 5 | Read a book | Read at least 50 pages of the new novel | 2024-06-05 |

### 3.6 Self-Healing Behavior

Kubernetes' Deployment controller continuously reconciles desired vs. actual state:

- **API pod deleted:** The ReplicaSet detects fewer than 4 running pods and schedules a new one. The replacement pod is running within ~20–30 seconds. During this period, the remaining 3 pods continue serving traffic.
- **Database pod deleted:** The Deployment controller reschedules the MSSQL pod. Because `/var/opt/mssql` is backed by the Azure File share (which persists independently of the pod), all data is intact when the replacement pod mounts the same PVC.

### 3.7 Rolling Updates

The API Deployment uses `strategy.type: RollingUpdate` (Kubernetes default). When a new image version is applied:

1. Kubernetes creates one new pod with the updated image.
2. Once the new pod is `Ready`, it terminates one old pod.
3. This continues until all pods are running the new version.
4. At no point does the available replica count drop to zero — traffic continues uninterrupted.

The MSSQL Deployment uses `strategy.type: Recreate`. This terminates the existing pod before creating a new one. This is correct for a single-instance stateful database because concurrent writes during an update would risk data corruption. Downtime during a database restart is accepted.

### 3.8 Local Development

A `docker-compose.yaml` file mirrors the Kubernetes topology for local development:

- MSSQL runs on port 1433 with a named volume for persistence.
- The API runs on port 8081 and uses the same environment variable names as the Kubernetes ConfigMap/Secret, ensuring configuration parity.
- The `depends_on` condition (`service_healthy`) ensures the API container waits for SQL Server to pass its health check before starting, preventing connection errors on cold start.

---

## 4. Justification for the Resources Utilized

This section covers the FinOps rationale: how resources were sized, why specific Kubernetes cost-control mechanisms were chosen, and where optimization opportunities exist.

### 4.1 Resource Requests and Limits

Requests and limits were set based on observed runtime behavior during local Docker Compose development and initial AKS deployment. They are not guesses — they reflect the actual footprint of each workload.

**API tier (`todo-api`):**

| | CPU | Memory |
|---|---|---|
| Request | 100m (0.1 core) | 128Mi |
| Limit | 500m (0.5 core) | 256Mi |

- The **100m CPU request** reflects observed baseline usage of a .NET Minimal API handling read-only SQL queries. Over-requesting CPU causes the scheduler to reserve more node capacity than needed, directly increasing the number of nodes (and cost) required.
- The **500m CPU limit** prevents a single pod from saturating a node core during a traffic burst. Rather than letting one pod consume more CPU, the HPA adds more pods — distributing load horizontally and keeping per-pod limits predictable.
- The **128Mi memory request** covers the .NET 10 runtime baseline (~80Mi) plus headroom for EF Core query result caching.
- The **256Mi memory limit** caps memory growth to prevent a pod from consuming node memory that would trigger evictions of co-located workloads, which would cause unnecessary rescheduling overhead.

**Database tier (`todo-mssql`):**

| | CPU | Memory |
|---|---|---|
| Request | 500m (0.5 core) | 2Gi |
| Limit | 1000m (1 core) | 3Gi |

- SQL Server 2022 requires a **minimum of 2Gi RAM** to start. The 2Gi request guarantees the scheduler only places the pod on a node that can satisfy this minimum, preventing OOMKill at startup and avoiding costly pod crash loops.
- The **3Gi memory limit** gives SQL Server headroom for its buffer pool (which it aggressively fills with cached data pages), while preventing it from consuming all available node memory and starving co-located API pods.
- Without a memory limit, SQL Server would consume all node memory, potentially triggering cluster-autoscaler scale-out on a node that is not actually CPU-starved — an invisible cost driver.

### 4.2 HorizontalPodAutoscaler — Eliminating Static Over-Provisioning

The `todo-api-hpa` scales the API Deployment between **4 and 10 replicas** based on CPU and memory utilization thresholds of 80%.

Without HPA, the Deployment would need to be statically sized for the worst-case peak traffic — likely 10 replicas running 24/7. With HPA:

- During low-traffic periods, only 4 pods run, consuming approximately 400m CPU and 512Mi memory total.
- During traffic spikes, Kubernetes adds pods within seconds and releases them when utilization drops.
- This eliminates the cost of idle capacity that would otherwise be permanently reserved.

Dual-metric scaling (CPU and memory) is more accurate than CPU-only for a .NET API that may be memory-bound under large query result sets, ensuring scale-up triggers before either resource becomes a bottleneck.

### 4.3 Cost Optimization Opportunities

**Opportunity 1 — Right-sized resource requests prevent over-provisioning**

The API pod requests 100m CPU and 128Mi memory. If requests were set conservatively high (e.g., 500m CPU / 512Mi memory "just to be safe"), each of the 4 pods would reserve 4× more node capacity than needed — requiring a more expensive node SKU or more nodes. Right-sizing requests to observed usage directly reduces the cluster's required capacity.

**Opportunity 2 — HPA eliminates static over-provisioning for peak load**

Without HPA, the API Deployment would be statically set to 10 replicas to handle peak traffic, consuming 10× the resources at all times. With HPA, the baseline is 4 replicas and additional pods are only provisioned when CPU or memory utilization actually exceeds 80%. Off-peak hours (nights, weekends) run at minimum cost.

**Opportunity 3 — `Retain` reclaim policy prevents accidental storage deletion costs**

Azure File storage data, once deleted, cannot be recovered without a backup. The `Retain` policy on the StorageClass ensures the Azure File share is **not automatically deleted** if the PVC is removed (e.g., by a `kubectl delete namespace`). This avoids the operational cost of restoring from backup or re-seeding the database, and eliminates the risk of permanent data loss that would require emergency spend.

**Opportunity 4 — Bounded database memory limit prevents unnecessary node scale-out**

SQL Server expands its buffer pool to consume all available memory if unconstrained. A 3Gi memory limit prevents the database pod from starving co-located API pods, which would trigger OOMKill events and pod rescheduling. Frequent rescheduling can trigger the cluster autoscaler to add nodes unnecessarily, increasing compute costs. The limit keeps the database's footprint predictable.