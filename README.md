# Todo App — Kubernetes Deployment (NAGP 2026)

## Links

| Resource | URL |
|---|---|
| Code Repository | _Add your GitHub/GitLab repo URL here_ |
| Docker Hub Image | https://hub.docker.com/r/dclearning/todo-api |
| Live API (May not work) | `http://20.204.212.246/todoitems` | 

---

## Architecture

```
External Client
      │
      ▼
 [ Ingress (AGIC) ]
      │  HTTP /todoitems
      ▼
 [ todo-api Service (ClusterIP:80) ]
      │
      ▼
 [ todo-api Pods ×4  (Deployment + HPA) ]
      │  TCP 1433 via DNS name
      ▼
 [ todo-mssql Service (ClusterIP:1433) ]
      │
      ▼
 [ todo-mssql Pod ×1 (Deployment) ]
      │
      ▼
 [ Azure File Share (PersistentVolume) ]
```

All inter-tier communication uses **Kubernetes Service DNS names**, never Pod IPs.

---

## Kubernetes Objects

| Object | File |
|---|---|
| Namespace | `k8s/manifests/namespaces/todo.namespace.yaml` |
| API Deployment (4 pods, RollingUpdate) | `k8s/manifests/deployments/todo-api.deployment.yaml` |
| MSSQL Deployment (1 pod, Recreate) | `k8s/manifests/deployments/todo-mssql.deployment.yaml` |
| API Service (ClusterIP) | `k8s/manifests/services/todo-api.svc.yaml` |
| MSSQL Service (ClusterIP) | `k8s/manifests/services/todo-mssql.svc.yaml` |
| Ingress (AGIC) | `k8s/manifests/ingresses/todo.ingress.yaml` |
| API ConfigMap | `k8s/manifests/config-maps/todo-api.configmap.yaml` |
| MSSQL ConfigMap | `k8s/manifests/config-maps/todo-mssql.configmap.yaml` |
| API Secret (DB password) | `k8s/manifests/secrets/todo-api.secret.yaml` |
| MSSQL Secret (SA password) | `k8s/manifests/secrets/todo-mssql.secret.yaml` |
| Azure Storage Secret | `k8s/manifests/secrets/azure.secret.yaml` |
| StorageClass (Azure File) | `k8s/manifests/storage-class/azurefile.sc.yaml` |
| PersistentVolume | `k8s/manifests/persistent-volumes/todo-mssql.pv.yaml` |
| PersistentVolumeClaim | `k8s/manifests/persistent-volume-claims/todo-mssql.pvc.yaml` |
| HorizontalPodAutoscaler | `k8s/manifests/hpas/todo-api.hpa.yaml` |

---

## Deploy to AKS

### Prerequisites
- AKS cluster with Application Gateway Ingress Controller (AGIC) enabled
- Azure File Share created (`todo-mssql-file-share` in resource group `NAGP`)
- `kubectl` configured against the cluster

### 1. Apply all manifests in order

```bash
kubectl apply -f k8s/manifests/namespaces/
kubectl apply -f k8s/manifests/storage-class/
kubectl apply -f k8s/manifests/secrets/
kubectl apply -f k8s/manifests/config-maps/
kubectl apply -f k8s/manifests/persistent-volumes/
kubectl apply -f k8s/manifests/persistent-volume-claims/
kubectl apply -f k8s/manifests/deployments/
kubectl apply -f k8s/manifests/services/
kubectl apply -f k8s/manifests/ingresses/
kubectl apply -f k8s/manifests/hpas/
```

### 2. Initialize the database

After the MSSQL pod is Running, execute the setup script:

```bash
# Get the MSSQL pod name
kubectl get pods -n todo -l app=todo-mssql

# Copy and run the SQL script
kubectl cp todo-database/SetupDB.sql todo/<mssql-pod-name>:/tmp/SetupDB.sql -n todo
kubectl exec -n todo <mssql-pod-name> -- \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<password>" -C -i /tmp/SetupDB.sql
```

### 3. Verify

```bash
# Check all pods are running
kubectl get pods -n todo

# Check HPA status
kubectl get hpa -n todo

# Test the API (port-forward if Ingress DNS is not configured)
kubectl port-forward svc/todo-api-svc 9090:80 -n todo
curl http://localhost:9090/todoitems
```

---

## Local Development (Docker Compose)

```bash
docker compose up -d

# Initialize the database (wait ~20s for MSSQL to be ready)
docker exec -i todo-mssql \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "todo@123" -C \
  -i /dev/stdin < todo-database/SetupDB.sql

curl http://localhost:8081/todoitems
```

---

## FinOps Considerations

1. **Right-sized resource requests/limits** on the API tier prevent over-provisioning (`cpu: 100m–500m`, `memory: 128Mi–256Mi`).
2. **Right-sized resource requests/limits** on the Database tier bound SQL Server memory consumption (`cpu: 500m–1000m`, `memory: 2Gi–3Gi`), preventing it from starving other pods while allowing headroom above the 2Gi startup minimum.
3. **HPA** scales pods down to 4 during low traffic and up to 10 under load, avoiding static over-provisioning.
4. **Azure File `Retain` reclaim policy** prevents accidental storage deletion (cost of data loss vs. storage cost).

---
