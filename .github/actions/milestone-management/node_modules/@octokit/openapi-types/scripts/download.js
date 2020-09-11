const { get } = require("https");
const fs = require("fs");

if (!process.env.VERSION) {
  throw new Error("VERSION is not set");
}

download(process.env.VERSION.replace(/^v/, "")).then(
  () => console.log("done"),
  console.error
);

function download(version) {
  const path = `cache/openapi-schema.json`;
  const url = `https://unpkg.com/@octokit/openapi@${version}/generated/api.github.com.json`;

  const file = fs.createWriteStream(path);

  console.log("Downloading %s", url);

  return new Promise((resolve, reject) => {
    get(url, (response) => {
      response.pipe(file);
      file
        .on("finish", () =>
          file.close((error) => {
            if (error) return reject(error);
            console.log("%s written", path);
            resolve();
          })
        )
        .on("error", (error) => reject(error.message));
    });
  });
}
