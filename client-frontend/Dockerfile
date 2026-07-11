# --- build the Angular app ---
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# --- serve the static bundle with nginx ---
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Angular's application builder emits into dist/<project>/browser
COPY --from=build /app/dist/client-frontend/browser /usr/share/nginx/html
EXPOSE 80
