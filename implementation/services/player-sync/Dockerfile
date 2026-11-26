FROM node:22-bookworm-slim
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev --ignore-scripts
COPY server.mjs app-files.mjs personal-files.mjs shared-records.mjs ./
ENV NODE_ENV=production PORT=8080 BACKUP_STORAGE=/data
RUN mkdir /data && chown node:node /data
USER node
EXPOSE 8080
CMD ["node", "server.mjs"]
