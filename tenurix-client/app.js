process.env.NODE_ENV = process.env.NODE_ENV || "production";
process.env.HOSTNAME = process.env.HOSTNAME || "0.0.0.0";
process.chdir(__dirname);

const fs = require("fs");
const path = require("path");

const standaloneServer = path.join(__dirname, ".next", "standalone", "server.js");

require(standaloneServer);