const path = require("path");
const MOD = require("./mod.json");

const userDataPath = process.env.CSII_USERDATAPATH;
if (!userDataPath) {
  throw new Error("CSII_USERDATAPATH is not set. Install the official Cities: Skylines II modding toolchain first.");
}

module.exports = {
  mode: "production",
  stats: "minimal",
  entry: {
    [MOD.id]: "./src/index.tsx",
  },
  externalsType: "window",
  externals: {
    react: "React",
    "react-dom": "ReactDOM",
    "cs2/modding": "cs2/modding",
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: "ts-loader",
        exclude: /node_modules/,
      },
    ],
  },
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
    modules: ["node_modules", path.join(__dirname, "src")],
  },
  output: {
    path: path.resolve(`${userDataPath}\\Mods\\${MOD.id}`),
    filename: `${MOD.id}.mjs`,
    library: { type: "module" },
    publicPath: "coui://ui-mods/",
  },
  experiments: {
    outputModule: true,
  },
};
