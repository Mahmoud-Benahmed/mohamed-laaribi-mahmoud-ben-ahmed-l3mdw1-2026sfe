kubectl apply -f k8s/namespace.yaml

# Create secrets first
# (run the kubectl create secret commands above)

# Infrastructure
kubectl apply -f k8s/infra/sqlserver.yaml
kubectl apply -f k8s/infra/mongodb.yaml
kubectl apply -f k8s/infra/kafka.yaml

# Wait for infra to be ready
kubectl wait --namespace=erp --for=condition=ready pod --selector=app=sqlserver --timeout=180s
kubectl wait --namespace=erp --for=condition=ready pod --selector=app=mongo --timeout=120s
kubectl wait --namespace=erp --for=condition=ready pod --selector=app=kafka --timeout=120s

# Services (initContainers handle dependency waits)
kubectl apply -f k8s/services/tenant-service.yaml
kubectl apply -f k8s/services/auth-service.yaml
kubectl apply -f k8s/services/article-service.yaml
kubectl apply -f k8s/services/client-service.yaml
kubectl apply -f k8s/services/fournisseur-service.yaml
kubectl apply -f k8s/services/stock-service.yaml
kubectl apply -f k8s/services/invoice-service.yaml
kubectl apply -f k8s/services/payment-service.yaml

# Gateway + Ingress last
kubectl apply -f k8s/gateway/gateway.yaml
kubectl apply -f k8s/ingress/ingress.yaml

# Add to hosts file (run as Administrator)
Add-Content C:\Windows\System32\drivers\etc\hosts "$(minikube ip) erp.local"
Add-Content C:\Windows\System32\drivers\etc\hosts "$(minikube ip) acme.erp.local"
Add-Content C:\Windows\System32\drivers\etc\hosts "$(minikube ip) xyz.erp.local"