# syntax=docker/dockerfile:1
# Build context: ./appointment.client

FROM node:20-alpine AS build
WORKDIR /src
COPY package*.json ./
RUN npm ci
COPY . ./
RUN npx ng build --configuration production

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/appointment.client/browser /usr/share/nginx/html
EXPOSE 80
