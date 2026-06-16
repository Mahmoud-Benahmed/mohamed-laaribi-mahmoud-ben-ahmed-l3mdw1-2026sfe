# ERP.Frontend/Dockerfile
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration=production

FROM alpine:latest
COPY --from=builder /app/dist/ERP.Frontend/browser /frontend
CMD ["sh", "-c", "cp -r /frontend/. /output/ && echo 'Done'"]