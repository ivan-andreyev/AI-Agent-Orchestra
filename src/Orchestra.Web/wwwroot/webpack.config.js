const path = require('path');

module.exports = {
  entry: './js/workflow-builder.js',
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: 'workflow-builder.bundle.js',
    library: 'WorkflowBuilder',
    libraryTarget: 'umd',
    globalObject: 'this'
  },
  module: {
    rules: [
      {
        test: /\.(js|jsx|ts|tsx)$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: {
            presets: ['@babel/preset-env', '@babel/preset-react']
          }
        }
      },
      {
        test: /\.ts$/,
        use: 'ts-loader',
        exclude: /node_modules/,
      },
      {
        test: /\.css$/,
        use: ['style-loader', 'css-loader']
      }
    ]
  },
  resolve: {
    extensions: ['.js', '.jsx', '.ts', '.tsx']
  },
  externals: {
    'react': 'React',
    'react-dom': 'ReactDOM'
  },
  mode: 'development'
};