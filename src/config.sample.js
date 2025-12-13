export const Config = {
    mail: {
        from: 'user@do.main',
        to: 'user@do.main',
        server: 'smtp.xamp.le',
        username: 'user',
        password: 'yolo'
    },
    feeds: [
        {
          name: 'hwsw',
          url: 'https://www.hwsw.hu/feed',
        },
        {
          name: '9to5mac',
          url: 'https://9to5mac.com/feed/',
        }
    ],
    fetchIntervalHours: 1 // New configuration for fetch interval
}
