const { writeFileSync } = require("fs");

if (!process.env.VERSION) {
  throw new Error("VERSION is not set");
}

const pkg = require("../package.json");

if (!pkg.octokit) {
  pkg.octokit = {};
}

pkg.octokit["openapi-version"] = process.env.VERSION.replace(/^v/, "");

writeFileSync("package.json", JSON.stringify(pkg, null, 2) + "\n");
