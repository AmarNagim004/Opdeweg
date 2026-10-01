module.exports = {
  preset: 'jest-expo',
  testMatch: ['**/__tests__/**/*.test.ts?(x)'],
  setupFiles: ['./jest.setup.js'],
  transformIgnorePatterns: [
    'node_modules/(?!((jest-)?react-native|@react-native(-community)?|expo(nent)?|@expo(nent)?/.*|@expo-google-fonts/.*|react-navigation|@react-navigation/.*|@livekit/.*|livekit-client|@microsoft/signalr|zustand))',
  ],
};
